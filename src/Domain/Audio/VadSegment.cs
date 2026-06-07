// The outcome of gating + trimming a captured clip with VAD: whether it contained speech at all
// (segments that are pure silence are gated out and never transcribed) and, when it did, the trimmed
// clip with leading/trailing silence removed and over-long internal pauses collapsed.

namespace Domain.Audio;

public sealed record VadSegment(bool ContainsSpeech, AudioClip Trimmed)
{
	/// <summary>A gated (silence-only) result carrying an empty clip at the given rate.</summary>
	public static VadSegment Silent(int sampleRate) => new(false, new AudioClip([], sampleRate));
}
