// Trims trailing dead air from a clip so the model isn't fed silence at the end, without clipping
// quiet word endings (WHISPER-112). The trailing region is removed only when it is a SUSTAINED run of
// sub-threshold samples (at least TrailingSilenceWindowMs); a shorter quiet tail is left intact because
// it is usually the soft end of speech (a trailing "s", a breathy consonant), not dead air. When a trim
// happens, a short pad of the recorded tail (TrailingPadMs) is kept beyond the last speech sample so the
// utterance never ends on a hard cut. A clip that is sub-threshold throughout trims to empty (no pad —
// there is nothing to transcribe).

using Application.Ports;
using Domain.Audio;

namespace Logic.AudioManagement;

public sealed class SilenceTrimmer(SilenceTrimmerOptions options) : ISilenceTrimmer
{
	public AudioClip Trim(AudioClip clip)
	{
		int total = clip.Samples.Count;
		int lastSpeechEnd = total;
		while (lastSpeechEnd > 0 && Math.Abs(clip.Samples[lastSpeechEnd - 1]) < options.AmplitudeThreshold)
		{
			lastSpeechEnd--;
		}

		// Sub-threshold throughout: nothing to transcribe, so no pad either.
		if (lastSpeechEnd == 0)
		{
			return clip with { Samples = [] };
		}

		// The quiet tail only counts as dead air when it is sustained; a shorter one is speech decay.
		int tailLength = total - lastSpeechEnd;
		if (tailLength < SamplesFor(options.TrailingSilenceWindowMs, clip.SampleRate))
		{
			return clip;
		}

		// Trim to the last speech plus a short pad of the actually-recorded tail (never synthesized).
		int keep = Math.Min(total, lastSpeechEnd + SamplesFor(options.TrailingPadMs, clip.SampleRate));
		if (keep == total)
		{
			return clip;
		}

		float[] trimmed = new float[keep];
		for (int i = 0; i < keep; i++)
		{
			trimmed[i] = clip.Samples[i];
		}

		return clip with { Samples = trimmed };
	}

	private static int SamplesFor(int milliseconds, int sampleRate) => milliseconds * sampleRate / 1000;
}
