using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Domain.Audio;
using Domain.Models;
using Infrastructure.Transcription;

namespace Inference.Worker;

internal sealed class NativeInferenceServer(Stream pipe, ComputeBackend backend) : IAsyncDisposable
{
	private NativeWhisperEngine? _engine;
	private string? _modelPath;
	private string? _language;

	public async Task<int> RunAsync(CancellationToken cancellationToken)
	{
		while (true)
		{
			InferenceWorkerRequest request;
			try
			{
				request = await InferencePipeProtocol.ReadRequestAsync(pipe, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex) when (ex is InvalidDataException or JsonException)
			{
				await TryRespondAsync(
					new InferenceWorkerResponse(false, Error: InferenceWorkerError.InvalidRequest, Detail: ex.Message),
					cancellationToken).ConfigureAwait(false);
				return 65;
			}
			catch (EndOfStreamException)
			{
				return 0;
			}

			if (request.Operation == InferenceOperation.Shutdown)
			{
				await InferencePipeProtocol.WriteResponseAsync(
					pipe,
					new InferenceWorkerResponse(true, new TranscriptionResult(string.Empty)),
					cancellationToken).ConfigureAwait(false);
				return 0;
			}

			try
			{
				InferenceWorkerResponse response = await ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
				await InferencePipeProtocol.WriteResponseAsync(pipe, response, cancellationToken).ConfigureAwait(false);
			}
			catch (ModelNotFoundException ex)
			{
				await TryRespondAsync(
					new InferenceWorkerResponse(false, Error: InferenceWorkerError.ModelNotFound, Detail: ex.Message),
					cancellationToken).ConfigureAwait(false);
			}
			catch (ModelLoadException ex)
			{
				await TryRespondAsync(
					new InferenceWorkerResponse(false, Error: InferenceWorkerError.ModelLoad, Detail: ex.Message),
					cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex) when (ex is SEHException or AccessViolationException or ExternalException)
			{
				await TryRespondAsync(
					new InferenceWorkerResponse(false, Error: InferenceWorkerError.NativeRuntime, Detail: ex.GetType().Name),
					cancellationToken).ConfigureAwait(false);
				return 70;
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				await TryRespondAsync(
					new InferenceWorkerResponse(false, Error: InferenceWorkerError.Unexpected, Detail: ex.GetType().Name),
					cancellationToken).ConfigureAwait(false);
				return 70;
			}
		}
	}

	private async ValueTask<InferenceWorkerResponse> ExecuteAsync(
		InferenceWorkerRequest request,
		CancellationToken cancellationToken)
	{
		if (request.Operation == InferenceOperation.Unload)
		{
			await ReleaseEngineAsync().ConfigureAwait(false);
			return new InferenceWorkerResponse(true, new TranscriptionResult(string.Empty));
		}

		await EnsureEngineAsync(request.ModelPath, request.Language).ConfigureAwait(false);
		if (request.Operation == InferenceOperation.Load)
		{
			return new InferenceWorkerResponse(true, new TranscriptionResult(string.Empty));
		}

		StringBuilder text = new();
		List<TranscriptionSegment> segments = [];
		await foreach (NativeWhisperSegment segment in _engine!
			.TranscribeAsync(request.Samples, request.DecodingOptions, cancellationToken)
			.ConfigureAwait(false))
		{
			text.Append(segment.Text);
			segments.Add(new TranscriptionSegment(
				segment.Text,
				segment.Start,
				segment.End,
				segment.Probability));
		}

		return new InferenceWorkerResponse(true, new TranscriptionResult(text.ToString().Trim(), segments));
	}

	private async ValueTask EnsureEngineAsync(string modelPath, string? language)
	{
		if (!File.Exists(modelPath))
		{
			throw new ModelNotFoundException(modelPath);
		}

		if (_engine is not null &&
			string.Equals(_modelPath, modelPath, StringComparison.OrdinalIgnoreCase) &&
			string.Equals(_language, language, StringComparison.OrdinalIgnoreCase))
		{
			return;
		}

		await ReleaseEngineAsync().ConfigureAwait(false);
		try
		{
			_engine = new NativeWhisperEngine(modelPath, backend, language);
			_modelPath = modelPath;
			_language = language;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new ModelLoadException(modelPath, ex);
		}
	}

	private async ValueTask ReleaseEngineAsync()
	{
		if (_engine is not null)
		{
			await _engine.DisposeAsync().ConfigureAwait(false);
			_engine = null;
			_modelPath = null;
			_language = null;
		}
	}

	private async ValueTask TryRespondAsync(InferenceWorkerResponse response, CancellationToken cancellationToken)
	{
		try
		{
			await InferencePipeProtocol.WriteResponseAsync(pipe, response, cancellationToken).ConfigureAwait(false);
		}
		catch (IOException)
		{
			// The parent is already gone; exiting this worker is the only safe recovery.
		}
	}

	public ValueTask DisposeAsync() => ReleaseEngineAsync();
}
