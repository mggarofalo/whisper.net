// The real capture client: wraps NAudio's WasapiCapture behind the IAudioCaptureClient seam. Its only
// job is device glue — pick the device to open from the user's saved selection (resolving a changed
// endpoint id by friendly name via DeviceSelectionPolicy, and following the OS default otherwise),
// negotiate the mix format, convert each device byte buffer to float samples, and translate NAudio's
// events to the seam's. All the capture *behavior* lives in WasapiAudioSource; this class is therefore
// validated by manual real-device smoke, not by the headless specs (which use a fake client instead).
// Opening the selected device can never break dictation: any failure to resolve or open it falls back
// to the OS default, exactly as before this class honored the selection.

using System.Runtime.InteropServices;
using Application.Ports;
using Domain.Audio;
using Domain.Settings;
using Logic.AppManagement.Settings;
using Logic.AudioManagement;
using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Infrastructure.Audio;

internal sealed class NAudioCaptureClient(
	IAudioDeviceEnumerator deviceEnumerator,
	DeviceSelectionPolicy selectionPolicy,
	SettingsHolder settings,
	ILogger<NAudioCaptureClient> logger) : IAudioCaptureClient
{
	private WasapiCapture? _capture;
	private MMDeviceEnumerator? _mmEnumerator;
	private MMDevice? _device;

	// A safe placeholder until Start negotiates the real device format.
	public CaptureFormat Format { get; private set; } = new(16_000, 1, 16, AudioSampleFormat.Pcm);

	public event EventHandler<AudioCaptureBuffer>? DataAvailable;
	public event EventHandler<AudioCaptureStopped>? RecordingStopped;

	public void Start()
	{
		WasapiCapture capture = OpenSelectedDevice();
		Format = ToCaptureFormat(capture.WaveFormat);

		capture.DataAvailable += OnDeviceData;
		capture.RecordingStopped += OnDeviceStopped;

		_capture = capture;
		capture.StartRecording();
	}

	// Open the device the user selected. The default-ctor WasapiCapture follows the OS default; a pinned
	// device is opened explicitly so the user's choice is actually honored (before this, capture always
	// used the OS default and ignored the selection). Resolution and opening are wrapped so any failure —
	// a stale id, a device that won't open — degrades to the OS default rather than failing the recording.
	private WasapiCapture OpenSelectedDevice()
	{
		try
		{
			AppSettings current = settings.Current;
			DeviceResolution resolution = selectionPolicy.Resolve(
				current.CaptureDeviceId,
				current.CaptureDeviceName,
				deviceEnumerator.GetCaptureDevices(),
				deviceEnumerator.GetSystemDefaultId());

			// Follow the OS default (sentinel selection) or a substituted/absent pin: the default-ctor
			// WasapiCapture tracks the current default, which is the desired behavior in all three cases.
			if (resolution.FollowsDefault || resolution.Substituted || resolution.DeviceId is null)
			{
				return new WasapiCapture();
			}

			_mmEnumerator = new MMDeviceEnumerator();
			_device = _mmEnumerator.GetDevice(resolution.DeviceId);
			logger.LogInformation("Capturing from the selected microphone \"{DeviceName}\".", _device.FriendlyName);
			return new WasapiCapture(_device);
		}
		catch (Exception exception)
		{
			logger.LogWarning(
				exception, "Could not open the selected microphone; falling back to the system default device.");
			DisposeDevice();
			return new WasapiCapture();
		}
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
		DisposeDevice();
	}

	// Release the explicitly-opened device (and the enumerator that produced it). A no-op when following
	// the OS default, where WasapiCapture owns its own device.
	private void DisposeDevice()
	{
		_device?.Dispose();
		_device = null;
		_mmEnumerator?.Dispose();
		_mmEnumerator = null;
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
		DisposeDevice();
	}
}
