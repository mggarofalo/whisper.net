// The IVad adapter: the device-independent windowing logic of voice-activity detection over the
// IVadSession seam. It slices the clip into fixed-size windows, scores each through the session, and
// returns the per-window probabilities. The threshold/gate/trim policy lives in Logic; this adapter
// only measures. Driven over a fake session in the @WHISPER-31 specs; OnnxVadSession supplies the
// real Silero model (manual smoke).

using Application.Ports;
using Domain.Audio;

namespace Infrastructure.Audio;

public sealed class SileroVad(IVadSession session) : IVad
{
	public ValueTask<VadAnalysis> AnalyzeAsync(AudioClip clip, CancellationToken cancellationToken)
	{
		int windowSamples = session.WindowSamples;
		IReadOnlyList<float> samples = clip.Samples;
		List<float> probabilities = [];

		session.Reset();

		// Score each full window; a trailing partial window (shorter than the model's window) is dropped.
		for (int start = 0; start + windowSamples <= samples.Count; start += windowSamples)
		{
			cancellationToken.ThrowIfCancellationRequested();

			float[] window = new float[windowSamples];
			for (int i = 0; i < windowSamples; i++)
			{
				window[i] = samples[start + i];
			}

			probabilities.Add(session.Next(window));
		}

		return ValueTask.FromResult(new VadAnalysis(probabilities, windowSamples));
	}
}
