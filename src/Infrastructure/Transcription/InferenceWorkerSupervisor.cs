// Owns the isolated Whisper worker process and the bounded recovery policy. A healthy generation is
// reused so its warmed model stays resident. A native failure replaces the whole process: first using
// the currently selected backend, then CPU-only if a replacement Vulkan worker also fails. The gate
// serializes requests because one worker owns one native model/context and one request/response pipe.

using Application.Ports;
using Domain.Audio;
using Domain.Models;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Transcription;

public sealed class InferenceWorkerSupervisor(
	IInferenceWorkerProcessFactory processFactory,
	IBackendSelector backendSelector,
	ILogger<InferenceWorkerSupervisor> logger) : IInferenceWorkerSupervisor
{
	private readonly SemaphoreSlim _gate = new(1, 1);
	private IInferenceWorkerProcess? _worker;
	private int _generation;
	private int _disposed;

	public int? CurrentGeneration => _worker?.Generation;
	public ComputeBackend? CurrentBackend => _worker?.Backend;
	public bool HasLiveWorker => _worker?.IsAlive == true;

	public async ValueTask<TranscriptionResult> TranscribeAsync(
		InferenceWorkerRequest request,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed != 0, this);
		await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			BackendSelection initialSelection = await backendSelector
				.SelectBackendAsync(cancellationToken)
				.ConfigureAwait(false);

			try
			{
				return await ExecuteAttemptAsync(request, initialSelection.Backend, cancellationToken).ConfigureAwait(false);
			}
			catch (InferenceWorkerUnavailableException firstFailure)
			{
				logger.LogWarning(
					firstFailure,
					"Inference worker generation {Generation} became unavailable; replacing it and retrying the request.",
					_worker?.Generation);
				await DiscardWorkerAsync().ConfigureAwait(false);

				BackendSelection recoverySelection = await backendSelector
					.SelectBackendAsync(cancellationToken)
					.ConfigureAwait(false);

				try
				{
					return await ExecuteAttemptAsync(request, recoverySelection.Backend, cancellationToken).ConfigureAwait(false);
				}
				catch (InferenceWorkerUnavailableException secondFailure)
					when (recoverySelection.Backend == ComputeBackend.Vulkan)
				{
					logger.LogWarning(
						secondFailure,
						"Replacement Vulkan worker generation {Generation} failed; falling back to a CPU-only worker.",
						_worker?.Generation);
					await DiscardWorkerAsync().ConfigureAwait(false);

					try
					{
						return await ExecuteAttemptAsync(request, ComputeBackend.Cpu, cancellationToken).ConfigureAwait(false);
					}
					catch (InferenceWorkerUnavailableException cpuFailure)
					{
						await DiscardWorkerAsync().ConfigureAwait(false);
						logger.LogError(cpuFailure, "CPU inference worker also failed; recovery is exhausted for this request.");
						throw Exhausted(cpuFailure);
					}
				}
				catch (InferenceWorkerUnavailableException recoveryFailure)
				{
					await DiscardWorkerAsync().ConfigureAwait(false);
					logger.LogError(recoveryFailure, "Inference worker recovery is exhausted for this request.");
					throw Exhausted(recoveryFailure);
				}
			}
		}
		catch (OperationCanceledException)
		{
			// A cancelled pipe exchange cannot be safely resumed: discard it so a late response cannot
			// be mistaken for the next request and so native inference cannot outlive cancellation.
			await DiscardWorkerAsync().ConfigureAwait(false);
			throw;
		}
		finally
		{
			_gate.Release();
		}
	}

	public async ValueTask StopAsync(CancellationToken cancellationToken)
	{
		await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (_worker is not null)
			{
				IInferenceWorkerProcess worker = _worker;
				_worker = null;
				try
				{
					await worker.StopAsync(cancellationToken).ConfigureAwait(false);
				}
				finally
				{
					// Dispose forcibly terminates a worker that ignored shutdown or whose graceful wait
					// was cancelled, so host teardown can never leave an orphan inference process.
					await worker.DisposeAsync().ConfigureAwait(false);
				}
			}
		}
		finally
		{
			_gate.Release();
		}
	}

	private async ValueTask<TranscriptionResult> ExecuteAttemptAsync(
		InferenceWorkerRequest request,
		ComputeBackend backend,
		CancellationToken cancellationToken)
	{
		// Keep a healthy worker even if a fresh probe would now prefer another backend. In particular,
		// CPU fallback is sticky for this process lifetime so a still-broken GPU is not retried on every
		// dictation; an unavailable worker or app restart causes backend selection to run again.
		if (_worker is null || !_worker.IsAlive)
		{
			await DiscardWorkerAsync().ConfigureAwait(false);
			_worker = await processFactory
				.StartAsync(backend, ++_generation, cancellationToken)
				.ConfigureAwait(false);
			logger.LogInformation(
				"Started inference worker generation {Generation} using {Backend}.",
				_worker.Generation,
				_worker.Backend);
		}

		return await _worker.TranscribeAsync(request, cancellationToken).ConfigureAwait(false);
	}

	private async ValueTask DiscardWorkerAsync()
	{
		if (_worker is null)
		{
			return;
		}

		try
		{
			await _worker.DisposeAsync().ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			logger.LogDebug(ex, "Discarding inference worker generation {Generation} reported an error.", _worker.Generation);
		}
		finally
		{
			_worker = null;
		}
	}

	private static InferenceUnavailableException Exhausted(Exception failure) =>
		new("Local speech inference is unavailable after worker restart and CPU fallback.", failure);

	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _disposed, 1) != 0)
		{
			return;
		}

		await StopAsync(CancellationToken.None).ConfigureAwait(false);
		_gate.Dispose();
	}
}
