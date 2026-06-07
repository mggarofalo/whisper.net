// Payload for IAudioSource.CaptureFailed: a typed reason the capture stopped unexpectedly, plus a
// human-readable message. Reporting failures as an event (rather than throwing on the capture thread)
// lets the pipeline react without an unhandled exception tearing down the recording flow.

using Domain.Audio;

namespace Application.Ports;

public sealed class AudioCaptureFailedEventArgs(AudioCaptureError error, string message) : EventArgs
{
	/// <summary>The classified reason capture stopped.</summary>
	public AudioCaptureError Error { get; } = error;

	/// <summary>A human-readable description of the failure.</summary>
	public string Message { get; } = message;
}
