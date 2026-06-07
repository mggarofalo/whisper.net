// Removes trailing near-silent samples from a clip so the model isn't fed dead air at the end. A
// sample counts as silence when its amplitude is below a small threshold. (The configurable
// duration-based threshold from the Python predecessor is a Module 2 concern; this is the minimal,
// correct behavior the delivery pipeline needs today.)

using Application.Ports;
using Domain.Audio;

namespace Logic.AudioManagement;

public sealed class SilenceTrimmer : ISilenceTrimmer
{
	private const float SilenceAmplitudeThreshold = 0.01f;

	public AudioClip Trim(AudioClip clip)
	{
		int end = clip.Samples.Count;
		while (end > 0 && Math.Abs(clip.Samples[end - 1]) < SilenceAmplitudeThreshold)
		{
			end--;
		}

		if (end == clip.Samples.Count)
		{
			return clip;
		}

		float[] trimmed = new float[end];
		for (int i = 0; i < end; i++)
		{
			trimmed[i] = clip.Samples[i];
		}

		return clip with { Samples = trimmed };
	}
}
