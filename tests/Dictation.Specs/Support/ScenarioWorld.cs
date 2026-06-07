// Scenario-scoped state shared between step definitions and the driver within a single scenario.
// Resolved from the per-scenario DI scope, so there is no static state to leak between scenarios.

using Application.Transcription;
using Domain.Audio;

namespace Dictation.Specs.Support;

public sealed class ScenarioWorld
{
	// The clip "captured" for this scenario. Its content is irrelevant while the transcriber is faked.
	public AudioClip CapturedClip { get; set; } = AudioClip.OneSecondOfSilence();

	public DeliveryResult? LastResult { get; set; }
}
