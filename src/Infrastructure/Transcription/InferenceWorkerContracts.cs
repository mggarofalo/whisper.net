using Domain.Audio;
using Domain.Models;

namespace Infrastructure.Transcription;

/// <summary>One local inference request sent to the isolated worker.</summary>
public sealed record InferenceWorkerRequest(
	string ModelPath,
	string? Language,
	DecodingOptions DecodingOptions,
	int SampleRate,
	float[] Samples,
	InferenceOperation Operation = InferenceOperation.Transcribe);

public enum InferenceOperation
{
	Load,
	WarmUp,
	Transcribe,
	Unload,
	Shutdown,
}

public enum InferenceWorkerError
{
	None,
	ModelNotFound,
	ModelLoad,
	NativeRuntime,
	InvalidRequest,
	Unexpected,
}

public sealed record InferenceWorkerResponse(
	bool Success,
	TranscriptionResult? Result = null,
	InferenceWorkerError Error = InferenceWorkerError.None,
	string? Detail = null);

/// <summary>A single supervised worker generation. Implementations own the child process and IPC channel.</summary>
public interface IInferenceWorkerProcess : IAsyncDisposable
{
	int Generation { get; }
	int ProcessId { get; }
	ComputeBackend Backend { get; }
	bool IsAlive { get; }

	ValueTask<TranscriptionResult> TranscribeAsync(
		InferenceWorkerRequest request,
		CancellationToken cancellationToken);

	ValueTask StopAsync(CancellationToken cancellationToken);
}

/// <summary>Creates an isolated worker fixed to one compute backend for its entire lifetime.</summary>
public interface IInferenceWorkerProcessFactory
{
	ValueTask<IInferenceWorkerProcess> StartAsync(
		ComputeBackend backend,
		int generation,
		CancellationToken cancellationToken);
}

/// <summary>
/// Signals a crashed worker, broken IPC channel, startup failure, or native failure reported by the
/// worker. The supervisor catches only this typed boundary failure; model/configuration errors pass through.
/// </summary>
public sealed class InferenceWorkerUnavailableException : Exception
{
	public InferenceWorkerUnavailableException(string message)
		: base(message)
	{
	}

	public InferenceWorkerUnavailableException(string message, Exception innerException)
		: base(message, innerException)
	{
	}
}

public interface IInferenceWorkerSupervisor : IAsyncDisposable
{
	int? CurrentGeneration { get; }
	ComputeBackend? CurrentBackend { get; }
	bool HasLiveWorker { get; }

	ValueTask<TranscriptionResult> TranscribeAsync(
		InferenceWorkerRequest request,
		CancellationToken cancellationToken);

	ValueTask StopAsync(CancellationToken cancellationToken);
}
