using Domain.Audio;
using Domain.Models;
using Infrastructure.Transcription;

namespace Dictation.Specs.Support;

internal sealed class FakeTranscriptionWorker(string text) : IInferenceWorkerSupervisor
{
	public bool NetworkAccessed => false;
	public int? CurrentGeneration => 1;
	public ComputeBackend? CurrentBackend => ComputeBackend.Cpu;
	public bool HasLiveWorker => true;

	public ValueTask<TranscriptionResult> TranscribeAsync(
		InferenceWorkerRequest request,
		CancellationToken cancellationToken) =>
		ValueTask.FromResult(new TranscriptionResult(text));

	public ValueTask StopAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
	public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
