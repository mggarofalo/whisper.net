// Internal seam over the native capture device (NAudio's WasapiCapture). Splitting the device glue
// out behind this interface lets WasapiAudioSource's coordination logic — idempotent start, flushing
// on stop, mapping device errors to typed failures — run and be tested without a real microphone:
// the BDD specs and unit tests feed a fake, while NAudioCaptureClient wraps the real device. The
// seam already speaks in float samples (the device-byte→float conversion is the wrapper's job), so
// everything above it is platform- and format-agnostic.

using Domain.Audio;

namespace Infrastructure.Audio;

public interface IAudioCaptureClient : IDisposable
{
	/// <summary>The format negotiated with the device once capture is configured.</summary>
	CaptureFormat Format { get; }

	/// <summary>Raised for each buffer of float samples the device produces.</summary>
	event EventHandler<AudioCaptureBuffer>? DataAvailable;

	/// <summary>
	/// Raised once when recording stops — cleanly (after <see cref="Stop"/> flushes pending frames)
	/// or because of a device error. Carries the error reason when the stop was not clean.
	/// </summary>
	event EventHandler<AudioCaptureStopped>? RecordingStopped;

	/// <summary>Opens the device and begins recording.</summary>
	void Start();

	/// <summary>Stops recording, flushing any pending frames, then raises a clean <see cref="RecordingStopped"/>.</summary>
	void Stop();
}

/// <summary>One buffer of interleaved float samples from the device.</summary>
public sealed record AudioCaptureBuffer(ReadOnlyMemory<float> Samples);

/// <summary>
/// The terminal recording-stopped signal. <see cref="Error"/> is <c>null</c> for a clean stop and set
/// when the device stopped because of a failure.
/// </summary>
public sealed record AudioCaptureStopped(AudioCaptureError? Error, string? Message);
