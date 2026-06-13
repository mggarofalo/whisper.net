// Trims trailing dead air from a clip so the model isn't fed silence at the end, without clipping quiet
// word endings. End-of-speech is found by ENERGY, not raw per-sample amplitude: the clip is
// scanned in short frames and the last frame whose RMS energy reaches the threshold is the end of speech.
// This is the fix for the reopened clip — a word trailing off has individual samples below the old
// per-sample bar yet still carries real frame energy, so it was wrongly trimmed as dead air; RMS over a
// window keeps quiet speech and cuts only genuine near-silence. The trailing region is removed only when
// it is a SUSTAINED run of sub-threshold frames (at least TrailingSilenceWindowMs); a shorter quiet tail
// is left intact because it is usually the soft end of speech. When a trim happens, a short pad of the
// recorded tail (TrailingPadMs) is kept beyond the last speech so the utterance never ends on a hard cut.
// A clip that is sub-threshold throughout trims to empty (no pad — there is nothing to transcribe).

using Application.Ports;
using Domain.Audio;

namespace Logic.AudioManagement;

public sealed class SilenceTrimmer(SilenceTrimmerOptions options) : ISilenceTrimmer
{
	public AudioClip Trim(AudioClip clip)
	{
		int total = clip.Samples.Count;
		if (total == 0)
		{
			return clip;
		}

		int frame = Math.Max(1, SamplesFor(options.FrameMs, clip.SampleRate));

		// Compare mean-square against the threshold SQUARED rather than RMS against the threshold: it is the
		// same boundary without a sqrt, so a frame whose energy sits exactly at the threshold is speech
		// deterministically (no float rounding to nudge it under).
		double thresholdMeanSquare = (double)options.EnergyThreshold * options.EnergyThreshold;

		// Walk the clip in frames and remember the end of the last frame that carries speech-level energy.
		// Frame-aligned, so a frame straddling the speech/silence boundary is kept (it still holds speech).
		int lastSpeechEnd = 0;
		for (int start = 0; start < total; start += frame)
		{
			int end = Math.Min(total, start + frame);
			if (FrameMeanSquare(clip.Samples, start, end) >= thresholdMeanSquare)
			{
				lastSpeechEnd = end;
			}
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

	// Mean-square energy of a frame [start, end) — RMS without the sqrt. Sign-independent, so a bipolar
	// quiet word reads as real energy while genuine near-silence sits near zero.
	private static double FrameMeanSquare(IReadOnlyList<float> samples, int start, int end)
	{
		double sumOfSquares = 0;
		for (int i = start; i < end; i++)
		{
			sumOfSquares += (double)samples[i] * samples[i];
		}

		int count = end - start;
		return count == 0 ? 0 : sumOfSquares / count;
	}

	private static int SamplesFor(int milliseconds, int sampleRate) => milliseconds * sampleRate / 1000;
}
