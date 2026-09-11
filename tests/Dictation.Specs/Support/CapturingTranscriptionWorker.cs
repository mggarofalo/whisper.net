using Domain.Audio;
using Domain.Models;
using Infrastructure.Transcription;

namespace Dictation.Specs.Support;

internal sealed class CapturingTranscriptionWorker : IInferenceWorkerSupervisor
{
	public int CallCount { get; private set; }
	public InferenceWorkerRequest? LastRequest { get; private set; }
	public int? CurrentGeneration => 1;
	public ComputeBackend? CurrentBackend => ComputeBackend.Cpu;
	public bool HasLiveWorker => true;

	public ValueTask<TranscriptionResult> TranscribeAsync(
		InferenceWorkerRequest request,
		CancellationToken cancellationToken)
	{
		CallCount++;
		LastRequest = request;
		return ValueTask.FromResult(new TranscriptionResult("ok"));
	}

	public ValueTask StopAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
	public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
