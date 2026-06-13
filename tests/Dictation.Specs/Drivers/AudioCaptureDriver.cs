// Drives the capture scenarios. It owns HOW capture is exercised so the step definitions
// stay one-liners: it configures the fake device, starts/stops the REAL WasapiAudioSource through the
// IAudioSource port, records the frames and failures the port raises, and asserts at that boundary.

using Application.Ports;
using AwesomeAssertions;
using Dictation.Specs.Support;
using Domain.Audio;

namespace Dictation.Specs.Drivers;

public sealed class AudioCaptureDriver
{
	private readonly IAudioSource _source;
	private readonly FakeAudioCaptureClient _device;
	private readonly List<AudioFrameAvailableEventArgs> _frames = [];
	private readonly List<AudioCaptureFailedEventArgs> _failures = [];

	public AudioCaptureDriver(IAudioSource source, FakeAudioCaptureClient device)
	{
		_source = source;
		_device = device;
		_source.FrameAvailable += (_, e) => _frames.Add(e);
		_source.CaptureFailed += (_, e) => _failures.Add(e);
	}

	public void DeviceFormat(int sampleRate, int channels) =>
		_device.Format = new CaptureFormat(sampleRate, channels, 32, AudioSampleFormat.IeeeFloat);

	public void BufferFramesToFlush(int count)
	{
		for (int i = 0; i < count; i++)
		{
			_device.BufferFlushFrame(new float[8]);
		}
	}

	public void Start() => _source.Start();

	public void ProduceFrame(int sampleCount) => _device.ProduceFrame(new float[sampleCount]);

	public void Stop() => _source.Stop();

	public void DeviceBecomesUnavailable() =>
		_device.Fail(AudioCaptureError.DeviceUnavailable, "Capture device removed.");

	// --- assertions (port boundary) ---

	public void AssertSingleFrameDelivered(int sampleCount, int sampleRate, int channels)
	{
		_frames.Should().ContainSingle();
		_frames[0].Samples.Length.Should().Be(sampleCount);
		_frames[0].Format.SampleRate.Should().Be(sampleRate);
		_frames[0].Format.Channels.Should().Be(channels);
	}

	public void AssertStartedOnce() => _device.StartCount.Should().Be(1);

	public void AssertFrameCount(int count) => _frames.Should().HaveCount(count);

	// After a stop the source has torn down, so a stray device frame must not reach the port.
	public void AssertNoFurtherFramesAfter(Action stray)
	{
		int before = _frames.Count;
		stray();
		_frames.Should().HaveCount(before);
	}

	public void ProduceStrayFrame() => AssertNoFurtherFramesAfter(() => _device.ProduceFrame(new float[8]));

	public void AssertDeviceReleased() => _device.Released.Should().BeTrue();

	public void AssertCaptureFailed(string reason)
	{
		_failures.Should().ContainSingle();
		_failures[0].Error.ToString().Should().Be(reason);
	}
}
