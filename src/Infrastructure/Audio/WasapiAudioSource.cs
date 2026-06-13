// The IAudioSource adapter: the device-independent coordination logic of microphone capture, sitting
// on top of the IAudioCaptureClient seam. It owns the capture contract the rest of the app depends
// on — start/stop with idempotency, re-raising device frames as port frames in the negotiated format,
// flushing pending frames on stop, and turning a device error into a typed CaptureFailed event
// instead of an exception on the capture thread. All of this runs without a real device (over a fake
// client), which is how the @WHISPER-7 specs exercise it; NAudioCaptureClient supplies the real one.

using Application.Ports;
using Domain.Audio;

namespace Infrastructure.Audio;

public sealed class WasapiAudioSource(IAudioCaptureClient client) : IAudioSource, IDisposable
{
	private bool _running;

	public CaptureFormat? Format { get; private set; }

	public event EventHandler<AudioFrameAvailableEventArgs>? FrameAvailable;
	public event EventHandler<AudioCaptureFailedEventArgs>? CaptureFailed;

	// Begins capture. A second call while already running is a no-op so callers can't double-open the
	// device. The device only negotiates its real mix format DURING Start (the NAudioCaptureClient exposes a
	// placeholder until then), so Format MUST be read AFTER client.Start() — reading it before captured the
	// first recording at the wrong sample rate (e.g. 48 kHz audio tagged 16 kHz, never downsampled, so it
	// played back 3x too slow and Whisper could not transcribe it), while every later recording happened to
	// reuse the previously-negotiated format and worked (WHISPER-132). Subscriptions are wired before Start so
	// no frame is missed; OnDataAvailable's _running/Format guards drop any frame that races the brief gap
	// between Start returning and the format being read (the first real buffer is milliseconds away).
	public void Start()
	{
		if (_running)
		{
			return;
		}

		client.DataAvailable += OnDataAvailable;
		client.RecordingStopped += OnRecordingStopped;
		client.Start();
		Format = client.Format;
		_running = true;
	}

	// Requests a stop; teardown (unsubscribe, clear Format, release) happens when the client reports
	// RecordingStopped, so frames flushed during the stop are still delivered first.
	public void Stop()
	{
		if (!_running)
		{
			return;
		}

		client.Stop();
	}

	// Re-raise each device buffer as a port frame in the negotiated format. Guarded so a stray frame
	// arriving after teardown is dropped rather than delivered with a null format.
	private void OnDataAvailable(object? sender, AudioCaptureBuffer buffer)
	{
		if (_running && Format is { } format)
		{
			FrameAvailable?.Invoke(this, new AudioFrameAvailableEventArgs(buffer.Samples, format));
		}
	}

	// Terminal signal for both clean and failed stops: tear down first, then surface a typed failure
	// if the stop was caused by a device error. Never throws back onto the capture thread.
	private void OnRecordingStopped(object? sender, AudioCaptureStopped stopped)
	{
		client.DataAvailable -= OnDataAvailable;
		client.RecordingStopped -= OnRecordingStopped;
		_running = false;
		Format = null;

		if (stopped.Error is { } error)
		{
			CaptureFailed?.Invoke(this, new AudioCaptureFailedEventArgs(error, stopped.Message ?? "Audio capture stopped unexpectedly."));
		}
	}

	public void Dispose() => client.Dispose();
}
