// A captured slice of mono PCM audio: the float samples plus their sample rate. This is the unit of
// audio that flows from capture, through silence trimming, into transcription.

namespace Domain.Audio;

public sealed record AudioClip(IReadOnlyList<float> Samples, int SampleRate)
{
	// A one-second clip of pure silence at 16 kHz — a convenient default for tests and for callers
	// that need an empty clip to start from.
	public static AudioClip OneSecondOfSilence(int sampleRate = 16_000) => new(new float[sampleRate], sampleRate);
}
