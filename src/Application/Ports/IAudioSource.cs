// Port for capturing microphone audio as a stream of raw PCM frames. Implemented in Infrastructure
// (NAudio/WASAPI in Module 2); faked in the BDD specs so the capture contract can be driven without a
// real input device. This is the low-level capture seam: it emits frames in the device's negotiated
// format. Turning those frames into a finalized 16 kHz mono AudioClip is a separate concern that
// lives in Logic.AudioManagement (the normalization/buffering stage).

using Domain.Audio;

namespace Application.Ports;

/// <summary>
/// Captures audio from the active input device, raising <see cref="FrameAvailable"/> for each buffer
/// of samples while running.
/// </summary>
/// <remarks>
/// <see cref="Start"/> opens the device and begins raising frame events; <see cref="Stop"/> ends
/// capture, flushes any pending frames, and releases the device. Frames are raised on a capture
/// thread, never blocking the caller. Capture errors surface through <see cref="CaptureFailed"/>
/// rather than throwing on that thread. Implementations are not required to be thread-safe — the
/// state manager owns the start/stop sequence and calls them from a single logical flow.
/// </remarks>
public interface IAudioSource
{
	/// <summary>
	/// The format negotiated with the device, available while capturing and <c>null</c> before
	/// <see cref="Start"/> / after <see cref="Stop"/>.
	/// </summary>
	CaptureFormat? Format { get; }

	/// <summary>Raised for each buffer of captured samples while running.</summary>
	event EventHandler<AudioFrameAvailableEventArgs>? FrameAvailable;

	/// <summary>Raised when capture stops because of a device error rather than a normal stop.</summary>
	event EventHandler<AudioCaptureFailedEventArgs>? CaptureFailed;

	/// <summary>Begins capturing from the active input device. Calling it while already running is a no-op.</summary>
	void Start();

	/// <summary>Stops capturing, flushes any pending frames, and releases the device. Idempotent.</summary>
	void Stop();
}
