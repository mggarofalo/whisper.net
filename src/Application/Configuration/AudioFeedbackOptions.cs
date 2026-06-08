// Strongly-typed binding for audio-feedback settings, populated from the "AudioFeedback" configuration
// section by AddApplication (WHISPER-21). Feedback is on by default; when disabled the orchestrator
// fires no cues, so no playback resource is ever allocated.

namespace Application.Configuration;

public sealed class AudioFeedbackOptions
{
	public const string SectionName = "AudioFeedback";

	/// <summary>Whether the pipeline plays a sound at recording start/stop and transcription complete.</summary>
	public bool Enabled { get; set; } = true;
}
