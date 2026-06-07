// The real capture client: wraps NAudio's WasapiCapture (shared mode, default capture device) behind
// the IAudioCaptureClient seam. Its only job is device glue — negotiate the mix format, convert each
// device byte buffer to float samples, and translate NAudio's events to the seam's. All the capture
// *behavior* lives in WasapiAudioSource; this class is therefore validated by manual real-device
// smoke, not by the headless specs (which use a fake client instead).

using System.Runtime.InteropServices;
using Domain.Audio;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Infrastructure.Audio;

internal sealed class NAudioCaptureClient : IAudioCaptureClient
{
	private WasapiCapture? _capture;

	// A safe placeholder until Start negotiates the real device format.
	public CaptureFormat Format { get; private set; } = new(16_000, 1, 16, AudioSampleFormat.Pcm);

	public event EventHandler<AudioCaptureBuffer>? DataAvailable;
	public event EventHandler<AudioCaptureStopped>? RecordingStopped;

	public void Start()
	{
		WasapiCapture capture = new();
		Format = ToCaptureFormat(capture.WaveFormat);

		capture.DataAvailable += OnDeviceData;
		capture.RecordingStopped += OnDeviceStopped;

		_capture = capture;
		capture.StartRecording();
	}

	public void Stop() => _capture?.StopRecording();

	private void OnDeviceData(object? sender, WaveInEventArgs e) =>
		DataAvailable?.Invoke(this, new AudioCaptureBuffer(ToFloatSamples(e.Buffer, e.BytesRecorded, Format)));

	private void OnDeviceStopped(object? sender, StoppedEventArgs e)
	{
		AudioCaptureError? error = e.Exception is null ? null : Classify(e.Exception);
		RecordingStopped?.Invoke(this, new AudioCaptureStopped(error, e.Exception?.Message));

		_capture?.Dispose();
		_capture = null;
	}

	// Map NAudio's WaveFormat to the port's CaptureFormat value object.
	private static CaptureFormat ToCaptureFormat(WaveFormat format)
	{
		AudioSampleFormat sampleFormat = format.Encoding == WaveFormatEncoding.IeeeFloat
			? AudioSampleFormat.IeeeFloat
			: AudioSampleFormat.Pcm;

		return new CaptureFormat(format.SampleRate, format.Channels, format.BitsPerSample, sampleFormat);
	}

	// Convert a device byte buffer to interleaved float samples. WASAPI shared mode is normally 32-bit
	// IEEE float; 16-bit PCM is handled as the common fallback.
	private static float[] ToFloatSamples(byte[] buffer, int bytesRecorded, CaptureFormat format)
	{
		if (format.SampleFormat == AudioSampleFormat.IeeeFloat && format.BitsPerSample == 32)
		{
			int count = bytesRecorded / sizeof(float);
			float[] samples = new float[count];
			MemoryMarshal.Cast<byte, float>(buffer.AsSpan(0, bytesRecorded)).CopyTo(samples);
			return samples;
		}

		if (format.BitsPerSample == 16)
		{
			int count = bytesRecorded / sizeof(short);
			float[] samples = new float[count];
			ReadOnlySpan<short> shorts = MemoryMarshal.Cast<byte, short>(buffer.AsSpan(0, bytesRecorded));
			for (int i = 0; i < count; i++)
			{
				samples[i] = shorts[i] / 32768f;
			}

			return samples;
		}

		// Unsupported bit depth: deliver nothing rather than garbage.
		return [];
	}

	// Classify a device exception into a typed capture error. A device disappearing surfaces as an
	// AudioClient HRESULT; everything else is reported as Unknown.
	private static AudioCaptureError Classify(Exception exception) =>
		exception is COMException
			? AudioCaptureError.DeviceUnavailable
			: AudioCaptureError.Unknown;

	public void Dispose()
	{
		_capture?.Dispose();
		_capture = null;
	}
}
