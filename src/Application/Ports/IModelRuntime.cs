// Port for the native side of the model lifecycle: loading a model into a usable handle, warming it up,
// transcribing through it, and releasing it. The lifecycle POLICY (load/unload/switch order, state,
// concurrency) lives in Logic.ModelManagement and drives this port; the actual Whisper.net work lives
// in Infrastructure. Faked in specs/tests so the policy can be driven without a real model.

using Domain.Audio;
using Domain.Models;

namespace Application.Ports;

public interface IModelRuntime
{
	/// <summary>Loads the requested model and returns a handle to it.</summary>
	ValueTask<IModelHandle> LoadAsync(ModelLoadRequest request, CancellationToken cancellationToken);
}

/// <summary>A loaded model. Disposing it releases the underlying native resources.</summary>
public interface IModelHandle : IAsyncDisposable
{
	/// <summary>Runs a tiny inference so the first real transcription is not penalized by lazy init.</summary>
	ValueTask WarmUpAsync(CancellationToken cancellationToken);

	/// <summary>Transcribes a clip against this loaded model.</summary>
	ValueTask<TranscriptionResult> TranscribeAsync(AudioClip clip, CancellationToken cancellationToken);
}
