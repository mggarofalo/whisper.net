// Port for managing the one expensive, stateful resource the app owns: the loaded Whisper model. It
// loads, unloads, and switches models (releasing the old before activating the new, so native handles
// never leak), warms a model up so the first utterance is not slow, applies the configured compute
// precision at load, and serializes transcription against switches so a transcription never runs on a
// half-loaded model. Current identity/state is observable for the UI. Implemented in
// Logic.ModelManagement.

using Domain.Audio;
using Domain.Models;

namespace Application.Ports;

public interface IModelLifecycle
{
	/// <summary>The model currently loaded (if any) and its state — observable for the UI.</summary>
	ModelStatus Status { get; }

	/// <summary>Loads <paramref name="modelId"/>, releasing any currently loaded model first.</summary>
	ValueTask LoadAsync(string modelId, CancellationToken cancellationToken);

	/// <summary>Switches to <paramref name="modelId"/>: releases the current model, then loads the new one.</summary>
	ValueTask SwitchAsync(string modelId, CancellationToken cancellationToken);

	/// <summary>Releases the current model, leaving nothing loaded.</summary>
	ValueTask UnloadAsync(CancellationToken cancellationToken);

	/// <summary>Transcribes against the loaded model, waiting for any in-flight load/switch to finish.</summary>
	ValueTask<TranscriptionResult> TranscribeAsync(AudioClip clip, CancellationToken cancellationToken);
}
