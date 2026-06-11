// Scenario-scoped state shared between step definitions and the driver within a single scenario.
// Resolved from the per-scenario DI scope, so there is no static state to leak between scenarios.

using System;
using Application.Transcription;
using Domain.Audio;

namespace Dictation.Specs.Support;

public sealed class ScenarioWorld
{
	// The clip "captured" for this scenario. It carries speech-level energy by default so delivery scenarios
	// pass the no-speech gate (WHISPER-125): the trimmer collapses silence to empty and the pipeline skips
	// transcription. Its exact content is otherwise irrelevant while the transcriber is faked; a scenario
	// testing silence sets this to AudioClip.OneSecondOfSilence() explicitly.
	public AudioClip CapturedClip { get; set; } = SpeechLevelClip();

	public DeliveryResult? LastResult { get; set; }

	private static AudioClip SpeechLevelClip()
	{
		float[] samples = new float[16_000];
		Array.Fill(samples, 0.1f);
		return new AudioClip(samples, 16_000);
	}
}
