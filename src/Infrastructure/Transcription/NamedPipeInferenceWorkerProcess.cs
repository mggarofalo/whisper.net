using System.Diagnostics;
using System.IO.Pipes;
using Domain.Audio;
using Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Transcription;

public sealed class InferenceWorkerOptions
{
	public const string SectionName = "InferenceWorker";
	public string ExecutablePath { get; set; } = Path.Combine(AppContext.BaseDirectory, "worker", "Inference.Worker.exe");
	public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(15);
	public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(3);
}

public sealed class NamedPipeInferenceWorkerProcessFactory(
	IOptions<InferenceWorkerOptions> options,
	ILogger<NamedPipeInferenceWorkerProcessFactory> logger) : IInferenceWorkerProcessFactory
{
	private readonly InferenceWorkerOptions _options = options.Value;

	public async ValueTask<IInferenceWorkerProcess> StartAsync(
		ComputeBackend backend,
		int generation,
		CancellationToken cancellationToken)
	{
		string executable = Path.GetFullPath(_options.ExecutablePath);
		if (!File.Exists(executable))
		{
			throw new InferenceWorkerUnavailableException($"Inference worker executable was not found at '{executable}'.");
		}

		string pipeName = $"whisper-net-{Environment.ProcessId}-{Guid.NewGuid():N}";
		ProcessStartInfo startInfo = new(executable)
		{
			UseShellExecute = false,
			CreateNoWindow = true,
			WindowStyle = ProcessWindowStyle.Hidden,
		};
		startInfo.ArgumentList.Add("--pipe");
		startInfo.ArgumentList.Add(pipeName);
		startInfo.ArgumentList.Add("--backend");
		startInfo.ArgumentList.Add(backend == ComputeBackend.Vulkan ? "vulkan" : "cpu");

		Process process = Process.Start(startInfo)
			?? throw new InferenceWorkerUnavailableException("Windows did not start the inference worker process.");
		NamedPipeClientStream pipe = new(
			".",
			pipeName,
			PipeDirection.InOut,
			PipeOptions.Asynchronous,
			System.Security.Principal.TokenImpersonationLevel.Identification);

		try
		{
			using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			timeout.CancelAfter(_options.ConnectTimeout);
			await pipe.ConnectAsync(timeout.Token).ConfigureAwait(false);
			logger.LogDebug("Connected to inference worker generation {Generation} on a private local pipe.", generation);
			return new NamedPipeInferenceWorkerProcess(process, pipe, generation, backend, _options.ShutdownTimeout);
		}
		catch (Exception ex)
		{
			pipe.Dispose();
			Terminate(process);
			process.Dispose();
			throw new InferenceWorkerUnavailableException("Could not connect to the inference worker.", ex);
		}
	}

	private static void Terminate(Process process)
	{
		try
		{
			if (!process.HasExited)
			{
				process.Kill(entireProcessTree: true);
			}
		}
		catch (InvalidOperationException)
		{
			// The process exited between HasExited and Kill.
		}
	}
}

internal sealed class NamedPipeInferenceWorkerProcess(
	Process process,
	NamedPipeClientStream pipe,
	int generation,
	ComputeBackend backend,
	TimeSpan shutdownTimeout) : IInferenceWorkerProcess
{
	private int _disposed;

	public int Generation { get; } = generation;
	public int ProcessId => process.Id;
	public ComputeBackend Backend { get; } = backend;
	public bool IsAlive => _disposed == 0 && !process.HasExited && pipe.IsConnected;

	public async ValueTask<TranscriptionResult> TranscribeAsync(
		InferenceWorkerRequest request,
		CancellationToken cancellationToken)
	{
		if (!IsAlive)
		{
			throw new InferenceWorkerUnavailableException($"Inference worker generation {Generation} is not running.");
		}

		try
		{
			await InferencePipeProtocol.WriteRequestAsync(pipe, request, cancellationToken).ConfigureAwait(false);
			InferenceWorkerResponse response = await InferencePipeProtocol.ReadResponseAsync(pipe, cancellationToken).ConfigureAwait(false);
			return Map(response, request.ModelPath);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex) when (ex is IOException or EndOfStreamException or ObjectDisposedException or InvalidDataException)
		{
			throw new InferenceWorkerUnavailableException(
				$"Lost the IPC channel to inference worker generation {Generation}.",
				ex);
		}
	}

	public async ValueTask StopAsync(CancellationToken cancellationToken)
	{
		if (!IsAlive)
		{
			return;
		}

		try
		{
			InferenceWorkerRequest shutdown = new(
				string.Empty,
				null,
				DecodingOptions.Default,
				1,
				[],
				InferenceOperation.Shutdown);
			await InferencePipeProtocol.WriteRequestAsync(pipe, shutdown, cancellationToken).ConfigureAwait(false);
			await InferencePipeProtocol.ReadResponseAsync(pipe, cancellationToken).ConfigureAwait(false);

			using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			timeout.CancelAfter(shutdownTimeout);
			await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			Terminate();
			throw;
		}
		catch (Exception)
		{
			Terminate();
		}
	}

	private static TranscriptionResult Map(InferenceWorkerResponse response, string modelPath)
	{
		if (response.Success && response.Result is not null)
		{
			return response.Result;
		}

		return response.Error switch
		{
			InferenceWorkerError.ModelNotFound => throw new ModelNotFoundException(modelPath),
			InferenceWorkerError.ModelLoad => throw new ModelLoadException(modelPath, new InvalidDataException(response.Detail)),
			InferenceWorkerError.NativeRuntime => throw new InferenceWorkerUnavailableException(response.Detail ?? "Native inference failed."),
			InferenceWorkerError.InvalidRequest => throw new InvalidDataException(response.Detail),
			_ => throw new InferenceWorkerUnavailableException(response.Detail ?? "The inference worker failed unexpectedly."),
		};
	}

	private void Terminate()
	{
		try
		{
			if (!process.HasExited)
			{
				process.Kill(entireProcessTree: true);
				process.WaitForExit((int)shutdownTimeout.TotalMilliseconds);
			}
		}
		catch (InvalidOperationException)
		{
			// Already exited.
		}
	}

	public ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _disposed, 1) == 0)
		{
			pipe.Dispose();
			Terminate();
			process.Dispose();
		}

		return ValueTask.CompletedTask;
	}
}
