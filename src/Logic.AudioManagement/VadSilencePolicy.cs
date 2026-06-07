// Turns per-window VAD probabilities into a gated, silence-trimmed segment. This is the deterministic
// policy half of voice-activity detection (the probabilities come from the Infrastructure Silero
// adapter): it gates out all-silence clips, trims leading silence down to a small preroll, drops
// trailing silence after the last speech, and collapses over-long internal pauses — never dropping
// the speech in between. Pure logic over the clip + analysis, fully unit-testable with synthetic
// probabilities.

using Domain.Audio;

namespace Logic.AudioManagement;

public sealed class VadSilencePolicy
{
	public VadSegment Apply(AudioClip clip, VadAnalysis analysis, VadOptions options)
	{
		IReadOnlyList<float> probabilities = analysis.WindowProbabilities;
		int windowSamples = analysis.WindowSamples;
		IReadOnlyList<float> samples = clip.Samples;

		// Classify windows and find the speech span.
		int firstSpeech = -1;
		int lastSpeech = -1;
		for (int w = 0; w < probabilities.Count; w++)
		{
			if (probabilities[w] >= options.SpeechThreshold)
			{
				firstSpeech = firstSpeech < 0 ? w : firstSpeech;
				lastSpeech = w;
			}
		}

		// No speech anywhere -> gate the segment out.
		if (firstSpeech < 0)
		{
			return VadSegment.Silent(clip.SampleRate);
		}

		int leadingKeep = options.LeadingKeepMs * clip.SampleRate / 1000;
		int collapse = options.MidSilenceCollapseMs * clip.SampleRate / 1000;
		List<float> output = [];

		// Leading silence: keep only the most recent `leadingKeep` samples before the first speech.
		int firstSpeechStart = firstSpeech * windowSamples;
		Append(output, samples, Math.Max(0, firstSpeechStart - leadingKeep), firstSpeechStart);

		// Walk the speech span; include speech windows whole, collapse long internal silence runs.
		int window = firstSpeech;
		while (window <= lastSpeech)
		{
			if (probabilities[window] >= options.SpeechThreshold)
			{
				int start = window * windowSamples;
				Append(output, samples, start, start + windowSamples);
				window++;
				continue;
			}

			// A run of silence windows fully enclosed by speech (so it ends at or before lastSpeech).
			int runStart = window;
			while (window <= lastSpeech && probabilities[window] < options.SpeechThreshold)
			{
				window++;
			}

			int runSamples = (window - runStart) * windowSamples;
			int keep = Math.Min(runSamples, collapse);
			int runStartSample = runStart * windowSamples;
			Append(output, samples, runStartSample, runStartSample + keep);
		}

		// Trailing silence after the last speech window is dropped entirely.
		return new VadSegment(true, new AudioClip(output.ToArray(), clip.SampleRate));
	}

	// Append samples[start, end) to the output, clamped to the available range.
	private static void Append(List<float> output, IReadOnlyList<float> samples, int start, int end)
	{
		start = Math.Max(0, start);
		end = Math.Min(end, samples.Count);
		for (int i = start; i < end; i++)
		{
			output.Add(samples[i]);
		}
	}
}
