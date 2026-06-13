// A controllable stand-in for the native capture device, implementing the Infrastructure capture
// seam. It lets the scenarios drive the REAL WasapiAudioSource without a microphone:
// the test decides the negotiated format, when frames arrive, what flushes on stop, and whether the
// device fails. The deferred-stop mode additionally models NAudio's asynchronous stop:
// Stop() returns immediately and the device keeps delivering its in-flight frames until the scenario
// completes the stop. Only the device glue is faked here — all the capture behavior under test is real.

using Domain.Audio;
using Infrastructure.Audio;

namespace Dictation.Specs.Support;

public sealed class FakeAudioCaptureClient : IAudioCaptureClient
{
	private readonly List<float[]> _flushOnStop = [];
	private bool _stopRequested;

	public CaptureFormat Format { get; set; } = new(48_000, 2, 32, AudioSampleFormat.IeeeFloat);
	public int StartCount { get; private set; }
	public bool IsRecording { get; private set; }
	public bool Released { get; private set; }

	/// <summary>
	/// Opt in to NAudio's real stop timing: Stop() only records the request, and the
	/// device keeps delivering frames until the scenario calls <see cref="CompleteStop"/>. The default
	/// stays the synchronous flush the scenarios pin.
	/// </summary>
	public bool DeferStopCompletion { get; set; }

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

		if (DeferStopCompletion)
		{
			_stopRequested = true;
			return;
		}

		CompleteStop();
	}

	// Finish a stop: deliver everything still buffered on the device, then report the clean stop. In
	// the default mode Stop() calls this directly; in deferred mode the scenario calls it after the
	// device has produced its tail frames, modeling NAudio's asynchronous stop.
	public void CompleteStop()
	{
		if (!IsRecording && !_stopRequested)
		{
			return;
		}

		_stopRequested = false;
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
