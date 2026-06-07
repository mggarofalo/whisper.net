// A controllable stand-in for the native capture device, implementing the Infrastructure capture
// seam. It lets the @WHISPER-7 scenarios drive the REAL WasapiAudioSource without a microphone:
// the test decides the negotiated format, when frames arrive, what flushes on stop, and whether the
// device fails. Only the device glue is faked here — all the capture behavior under test is real.

using Domain.Audio;
using Infrastructure.Audio;

namespace Dictation.Specs.Support;

public sealed class FakeAudioCaptureClient : IAudioCaptureClient
{
	private readonly List<float[]> _flushOnStop = [];

	public CaptureFormat Format { get; set; } = new(48_000, 2, 32, AudioSampleFormat.IeeeFloat);
	public int StartCount { get; private set; }
	public bool IsRecording { get; private set; }
	public bool Released { get; private set; }

	public event EventHandler<AudioCaptureBuffer>? DataAvailable;
	public event EventHandler<AudioCaptureStopped>? RecordingStopped;

	public void Start()
	{
		StartCount++;
		IsRecording = true;
	}

	// Deliver one buffer of samples as if the device produced it.
	public void ProduceFrame(float[] samples) =>
		DataAvailable?.Invoke(this, new AudioCaptureBuffer(samples));

	// Queue a buffer that will be flushed (delivered) when Stop is called.
	public void BufferFlushFrame(float[] samples) => _flushOnStop.Add(samples);

	public void Stop()
	{
		if (!IsRecording)
		{
			return;
		}

		IsRecording = false;
		Released = true;

		foreach (float[] frame in _flushOnStop)
		{
			DataAvailable?.Invoke(this, new AudioCaptureBuffer(frame));
		}

		RecordingStopped?.Invoke(this, new AudioCaptureStopped(Error: null, Message: null));
	}

	// Simulate the device disappearing mid-session: a non-clean RecordingStopped with an error.
	public void Fail(AudioCaptureError error, string message)
	{
		IsRecording = false;
		RecordingStopped?.Invoke(this, new AudioCaptureStopped(error, message));
	}

	public void Dispose() => Released = true;
}
