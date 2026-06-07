// Converts a buffer of captured frames — interleaved float samples at the device's rate and channel
// count — into the mono, target-rate float stream Whisper expects. Two steps: average the channels
// down to mono, then linearly resample to the target rate. Pure logic: no device, no I/O, fully
// unit-testable by feeding synthetic samples.

using Domain.Audio;

namespace Logic.AudioManagement;

public sealed class AudioResampler
{
	/// <summary>
	/// Downmix <paramref name="interleaved"/> (laid out per <paramref name="source"/>'s channel count)
	/// to mono and resample it to <paramref name="targetSampleRate"/>.
	/// </summary>
	public float[] ToMono(ReadOnlySpan<float> interleaved, CaptureFormat source, int targetSampleRate)
	{
		if (source.Channels <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(source), "Capture format must have at least one channel.");
		}

		float[] mono = Downmix(interleaved, source.Channels);

		return source.SampleRate == targetSampleRate
			? mono
			: Resample(mono, source.SampleRate, targetSampleRate);
	}

	// Average all channels of each frame into a single mono sample.
	private static float[] Downmix(ReadOnlySpan<float> interleaved, int channels)
	{
		if (channels == 1)
		{
			return interleaved.ToArray();
		}

		int frames = interleaved.Length / channels;
		float[] mono = new float[frames];

		for (int frame = 0; frame < frames; frame++)
		{
			float sum = 0f;
			int baseIndex = frame * channels;
			for (int channel = 0; channel < channels; channel++)
			{
				sum += interleaved[baseIndex + channel];
			}

			mono[frame] = sum / channels;
		}

		return mono;
	}

	// Linear interpolation from sourceRate to targetRate. Adequate for speech fed to VAD/Whisper.
	private static float[] Resample(float[] mono, int sourceRate, int targetRate)
	{
		if (mono.Length == 0)
		{
			return mono;
		}

		int outputLength = (int)((long)mono.Length * targetRate / sourceRate);
		if (outputLength <= 0)
		{
			return [];
		}

		float[] output = new float[outputLength];
		double step = (double)sourceRate / targetRate;

		for (int i = 0; i < outputLength; i++)
		{
			double position = i * step;
			int left = (int)position;
			double fraction = position - left;
			int right = Math.Min(left + 1, mono.Length - 1);
			output[i] = (float)((mono[left] * (1 - fraction)) + (mono[right] * fraction));
		}

		return output;
	}
}
