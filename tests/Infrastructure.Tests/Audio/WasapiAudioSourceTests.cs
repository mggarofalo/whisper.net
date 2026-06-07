// Edge-case depth for the WHISPER-7 capture adapter, beyond the @WHISPER-7 acceptance scenarios.
// Drives the real WasapiAudioSource over a fake capture client (no device) and pins down the format
// lifecycle, sample-content fidelity, clean-stop semantics, and pre-start / not-started guards.

using Application.Ports;
using AwesomeAssertions;
using Domain.Audio;
using Infrastructure.Audio;
using Xunit;

namespace Infrastructure.Tests.Audio;

public sealed class WasapiAudioSourceTests
{
	// A minimal, hand-driven capture device for the unit tests.
	private sealed class FakeClient : IAudioCaptureClient
	{
		private readonly List<float[]> _flush = [];

		public CaptureFormat Format { get; set; } = new(48_000, 2, 32, AudioSampleFormat.IeeeFloat);
		public bool IsRecording { get; private set; }
		public bool Released { get; private set; }

		public event EventHandler<AudioCaptureBuffer>? DataAvailable;
		public event EventHandler<AudioCaptureStopped>? RecordingStopped;

		public void Start() => IsRecording = true;

		public void Produce(params float[] samples) => DataAvailable?.Invoke(this, new AudioCaptureBuffer(samples));

		public void QueueFlush(float[] samples) => _flush.Add(samples);

		public void Stop()
		{
			if (!IsRecording)
			{
				return;
			}

			IsRecording = false;
			Released = true;
			foreach (float[] frame in _flush)
			{
				DataAvailable?.Invoke(this, new AudioCaptureBuffer(frame));
			}

			RecordingStopped?.Invoke(this, new AudioCaptureStopped(null, null));
		}

		public void Fail(AudioCaptureError error) =>
			RecordingStopped?.Invoke(this, new AudioCaptureStopped(error, "boom"));

		public void Dispose() => Released = true;
	}

	[Fact]
	public void Format_is_null_before_start()
	{
		WasapiAudioSource source = new(new FakeClient());

		source.Format.Should().BeNull();
	}

	[Fact]
	public void Format_is_the_negotiated_device_format_while_running()
	{
		FakeClient client = new() { Format = new CaptureFormat(44_100, 1, 16, AudioSampleFormat.Pcm) };
		WasapiAudioSource source = new(client);

		source.Start();

		source.Format.Should().Be(new CaptureFormat(44_100, 1, 16, AudioSampleFormat.Pcm));
	}

	[Fact]
	public void Format_is_cleared_after_a_clean_stop()
	{
		FakeClient client = new();
		WasapiAudioSource source = new(client);

		source.Start();
		source.Stop();

		source.Format.Should().BeNull();
	}

	[Fact]
	public void Delivered_frame_preserves_the_device_samples()
	{
		FakeClient client = new();
		WasapiAudioSource source = new(client);
		AudioFrameAvailableEventArgs? captured = null;
		source.FrameAvailable += (_, e) => captured = e;

		source.Start();
		client.Produce(0.1f, -0.2f, 0.3f, -0.4f);

		captured.Should().NotBeNull();
		captured!.Samples.ToArray().Should().Equal(0.1f, -0.2f, 0.3f, -0.4f);
	}

	[Fact]
	public void Frames_before_start_are_not_delivered()
	{
		FakeClient client = new();
		WasapiAudioSource source = new(client);
		int frames = 0;
		source.FrameAvailable += (_, _) => frames++;

		client.Produce(0.5f); // device chatter before Start subscribes

		frames.Should().Be(0);
	}

	[Fact]
	public void A_clean_stop_does_not_report_a_capture_failure()
	{
		FakeClient client = new();
		WasapiAudioSource source = new(client);
		bool failed = false;
		source.CaptureFailed += (_, _) => failed = true;

		source.Start();
		source.Stop();

		failed.Should().BeFalse();
	}

	[Fact]
	public void Stopping_a_source_that_never_started_is_a_no_op()
	{
		FakeClient client = new();
		WasapiAudioSource source = new(client);

		Action stop = source.Stop;

		stop.Should().NotThrow();
		client.Released.Should().BeFalse();
	}

	[Fact]
	public void A_device_failure_clears_the_format_and_reports_once()
	{
		FakeClient client = new();
		WasapiAudioSource source = new(client);
		List<AudioCaptureError> errors = [];
		source.CaptureFailed += (_, e) => errors.Add(e.Error);

		source.Start();
		client.Fail(AudioCaptureError.ExclusiveModeDenied);

		errors.Should().ContainSingle().Which.Should().Be(AudioCaptureError.ExclusiveModeDenied);
		source.Format.Should().BeNull();
	}
}
