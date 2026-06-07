// The negotiated format of a capture device: how the raw samples coming off the microphone are laid
// out. The capture port surfaces this so a downstream normalization stage knows what it received
// (e.g. 48 kHz stereo) before resampling it to the 16 kHz mono float Whisper expects.

namespace Domain.Audio;

/// <summary>How a captured sample buffer encodes its values.</summary>
public enum AudioSampleFormat
{
	/// <summary>Signed integer PCM (e.g. 16-bit little-endian).</summary>
	Pcm,

	/// <summary>32-bit IEEE floating-point samples (the typical WASAPI shared-mode mix format).</summary>
	IeeeFloat,
}

/// <summary>
/// The format negotiated with a capture device: sample rate, channel count, bit depth, and sample
/// encoding. Frames delivered by an <c>IAudioSource</c> carry float samples interleaved per this
/// channel count and sampled at this rate.
/// </summary>
public sealed record CaptureFormat(int SampleRate, int Channels, int BitsPerSample, AudioSampleFormat SampleFormat)
{
	/// <summary>Bytes occupied by one sample across all channels in the device's native encoding.</summary>
	public int BlockAlign => Channels * (BitsPerSample / 8);
}
