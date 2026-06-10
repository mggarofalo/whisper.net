// Inner TDD loop for CaptureBuffer: the preroll ring retains only the most recent samples, recording
// is seeded with that preroll, the soft max-duration limit (WHISPER-111) signals once at 80% and once
// at 100% while every sample — including those past the limit — is retained, the hard failsafe limit
// signals once at the configured ceiling, the buffer is reusable across recordings (all limit signals
// re-arm per recording), a discard abandons the capture without materializing it, absurd configured
// durations clamp instead of wrapping the sample arithmetic, and the recording store survives the
// capture-thread/orchestrator-thread interleaving (appends racing a stop). Uses a 1 kHz target rate
// so 1 sample == 1 ms and the arithmetic is obvious.

using AwesomeAssertions;
using Domain.Audio;
using Xunit;

namespace Logic.AudioManagement.Tests;

public sealed class CaptureBufferTests
{
	private const int Rate = 1_000;

	private static CaptureBuffer NewBuffer(int prerollMs, int maxMs, int hardMaxMs = 1_200_000) =>
		new(new AudioBufferingOptions(prerollMs, maxMs, TargetSampleRate: Rate, HardMaxDurationMs: hardMaxMs), new AudioResampler());

	private static void Append(CaptureBuffer buffer, params float[] samples) =>
		buffer.Append(samples, new CaptureFormat(Rate, 1, 32, AudioSampleFormat.IeeeFloat));

	[Fact]
	public void Preroll_ring_retains_only_the_most_recent_samples()
	{
		CaptureBuffer buffer = NewBuffer(prerollMs: 3, maxMs: 100);

		Append(buffer, 1f, 2f, 3f, 4f, 5f); // ring capacity 3 -> keeps 3,4,5
		buffer.StartRecording();
		AudioClip clip = buffer.StopRecording();

		clip.Samples.Should().Equal(3f, 4f, 5f);
	}

	[Fact]
	public void Recording_is_seeded_with_the_preroll_then_accumulates()
	{
		CaptureBuffer buffer = NewBuffer(prerollMs: 2, maxMs: 100);

		Append(buffer, 1f, 2f);   // preroll -> 1,2
		buffer.StartRecording();
		Append(buffer, 3f, 4f);   // recorded
		AudioClip clip = buffer.StopRecording();

		clip.Samples.Should().Equal(1f, 2f, 3f, 4f);
	}

	[Fact]
	public void Idle_audio_does_not_accumulate_into_a_recording()
	{
		CaptureBuffer buffer = NewBuffer(prerollMs: 0, maxMs: 100);

		Append(buffer, 1f, 2f, 3f); // idle, no preroll retained
		buffer.StartRecording();
		AudioClip clip = buffer.StopRecording();

		clip.Samples.Should().BeEmpty();
	}

	[Fact]
	public void The_soft_limit_fires_once_and_recording_continues()
	{
		CaptureBuffer buffer = NewBuffer(prerollMs: 0, maxMs: 3);
		int fired = 0;
		buffer.MaxDurationReached += (_, _) => fired++;

		buffer.StartRecording();
		Append(buffer, 1f, 2f, 3f, 4f, 5f); // exceeds the 3-sample soft limit
		AudioClip clip = buffer.StopRecording();

		clip.Samples.Should().Equal(1f, 2f, 3f, 4f, 5f);
		fired.Should().Be(1);
	}

	[Fact]
	public void Appends_after_the_soft_limit_are_retained()
	{
		CaptureBuffer buffer = NewBuffer(prerollMs: 0, maxMs: 3);

		buffer.StartRecording();
		Append(buffer, 1f, 2f, 3f);
		Append(buffer, 4f, 5f); // past the soft limit: still retained, never dropped
		AudioClip clip = buffer.StopRecording();

		clip.Samples.Should().Equal(1f, 2f, 3f, 4f, 5f);
	}

	[Fact]
	public void Near_max_duration_fires_once_at_eighty_percent_of_the_limit()
	{
		CaptureBuffer buffer = NewBuffer(prerollMs: 0, maxMs: 10); // near threshold at 8 samples
		int fired = 0;
		buffer.NearMaxDuration += (_, _) => fired++;

		buffer.StartRecording();
		Append(buffer, 1f, 2f, 3f, 4f, 5f, 6f, 7f);
		fired.Should().Be(0, "the recording is still below 80% of the limit");

		Append(buffer, 8f); // exactly 80%
		fired.Should().Be(1);

		Append(buffer, 9f, 10f, 11f); // through and past the limit
		fired.Should().Be(1, "the near-limit warning fires once per recording");
	}

	[Fact]
	public void Max_duration_reached_fires_once_at_the_limit()
	{
		CaptureBuffer buffer = NewBuffer(prerollMs: 0, maxMs: 3);
		int fired = 0;
		buffer.MaxDurationReached += (_, _) => fired++;

		buffer.StartRecording();
		Append(buffer, 1f, 2f);
		fired.Should().Be(0, "the recording is still below the limit");

		Append(buffer, 3f); // exactly 100%
		fired.Should().Be(1);

		Append(buffer, 4f, 5f); // past the limit
		fired.Should().Be(1, "the at-limit signal fires once per recording");
	}

	[Fact]
	public void Limit_signals_re_arm_for_the_next_recording()
	{
		CaptureBuffer buffer = NewBuffer(prerollMs: 0, maxMs: 5); // near threshold at 4 samples
		int near = 0;
		int reached = 0;
		buffer.NearMaxDuration += (_, _) => near++;
		buffer.MaxDurationReached += (_, _) => reached++;

		buffer.StartRecording();
		Append(buffer, 1f, 2f, 3f, 4f, 5f, 6f);
		buffer.StopRecording();

		buffer.StartRecording();
		Append(buffer, 1f, 2f, 3f, 4f, 5f, 6f);
		AudioClip second = buffer.StopRecording();

		near.Should().Be(2, "each recording warns once as it approaches the limit");
		reached.Should().Be(2, "each recording signals once at the limit");
		second.Samples.Should().Equal(1f, 2f, 3f, 4f, 5f, 6f);
	}

	[Fact]
	public void Stop_recording_returns_everything_recorded_past_the_limit()
	{
		CaptureBuffer buffer = NewBuffer(prerollMs: 0, maxMs: 2);

		buffer.StartRecording();
		Append(buffer, 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f); // four times the soft limit
		AudioClip clip = buffer.StopRecording();

		clip.Samples.Should().Equal(1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f);
	}

	[Fact]
	public void Is_reusable_across_recordings()
	{
		CaptureBuffer buffer = NewBuffer(prerollMs: 0, maxMs: 100);

		buffer.StartRecording();
		Append(buffer, 1f, 2f);
		buffer.StopRecording();

		buffer.StartRecording();
		Append(buffer, 9f);
		AudioClip second = buffer.StopRecording();

		second.Samples.Should().Equal(9f);
	}

	[Fact]
	public void Start_recording_is_idempotent()
	{
		CaptureBuffer buffer = NewBuffer(prerollMs: 0, maxMs: 100);

		buffer.StartRecording();
		Append(buffer, 1f);
		buffer.StartRecording(); // no-op: must not reset the in-progress recording
		Append(buffer, 2f);
		AudioClip clip = buffer.StopRecording();

		clip.Samples.Should().Equal(1f, 2f);
	}

	[Fact]
	public void The_hard_limit_fires_once_when_the_recording_reaches_it()
	{
		CaptureBuffer buffer = NewBuffer(prerollMs: 0, maxMs: 3, hardMaxMs: 6);
		int fired = 0;
		buffer.HardLimitReached += (_, _) => fired++;

		buffer.StartRecording();
		Append(buffer, 1f, 2f, 3f, 4f, 5f);
		fired.Should().Be(0, "the recording is still below the hard limit");

		Append(buffer, 6f); // exactly the hard ceiling
		fired.Should().Be(1);

		Append(buffer, 7f, 8f);
		fired.Should().Be(1, "the hard-limit failsafe fires once per recording");
	}

	[Fact]
	public void The_hard_limit_re_arms_for_the_next_recording()
	{
		CaptureBuffer buffer = NewBuffer(prerollMs: 0, maxMs: 2, hardMaxMs: 4);
		int fired = 0;
		buffer.HardLimitReached += (_, _) => fired++;

		buffer.StartRecording();
		Append(buffer, 1f, 2f, 3f, 4f);
		buffer.StopRecording();

		buffer.StartRecording();
		Append(buffer, 1f, 2f, 3f, 4f);
		buffer.StopRecording();

		fired.Should().Be(2, "each recording gets its own hard-limit failsafe");
	}

	[Fact]
	public void Discard_recording_abandons_the_capture_and_resets_for_reuse()
	{
		CaptureBuffer buffer = NewBuffer(prerollMs: 0, maxMs: 100);

		buffer.StartRecording();
		Append(buffer, 1f, 2f, 3f);
		buffer.DiscardRecording();
		buffer.IsRecording.Should().BeFalse("a discard ends the recording like a stop does");

		buffer.StartRecording();
		Append(buffer, 9f);
		AudioClip next = buffer.StopRecording();

		next.Samples.Should().Equal(9f);
	}

	[Fact]
	public void Discarding_while_idle_is_a_harmless_no_op()
	{
		CaptureBuffer buffer = NewBuffer(prerollMs: 2, maxMs: 100);

		Append(buffer, 1f, 2f); // preroll only
		buffer.DiscardRecording();

		buffer.StartRecording();
		AudioClip clip = buffer.StopRecording();

		// An idle discard must not disturb the retained preroll.
		clip.Samples.Should().Equal(1f, 2f);
	}

	[Fact]
	public void An_absurdly_large_configured_limit_is_clamped_instead_of_wrapping()
	{
		// (int)(int.MaxValue ms * 48 kHz / 1000) wraps negative without a clamp: the buffer would
		// construct a negative-capacity list (throwing) or fire every limit signal on the first sample.
		CaptureBuffer buffer = new(
			new AudioBufferingOptions(PrerollMs: 0, MaxDurationMs: int.MaxValue, TargetSampleRate: 48_000, HardMaxDurationMs: int.MaxValue),
			new AudioResampler());
		int fired = 0;
		buffer.NearMaxDuration += (_, _) => fired++;
		buffer.MaxDurationReached += (_, _) => fired++;
		buffer.HardLimitReached += (_, _) => fired++;

		buffer.StartRecording();
		buffer.Append([1f], new CaptureFormat(48_000, 1, 32, AudioSampleFormat.IeeeFloat));

		fired.Should().Be(0, "the clamped limits sit at the array ceiling, far beyond one sample");
	}

	[Fact]
	public async Task Concurrent_appends_and_a_stop_neither_throw_nor_tear_the_clip()
	{
		// The capture thread appends while the orchestrator thread finalizes — frames keep flowing
		// through the post-release grace window (WHISPER-112), and a cancel stops mid-stream. Hammer
		// that interleaving: every Append must land atomically (the snapshot holds whole frames only,
		// every sample intact) and nothing may throw.
		const int frameSize = 8;
		const int framesPerIteration = 64;
		CancellationToken cancellation = TestContext.Current.CancellationToken;
		float[] frame = new float[frameSize];
		Array.Fill(frame, 1f);

		for (int iteration = 0; iteration < 200; iteration++)
		{
			CaptureBuffer buffer = NewBuffer(prerollMs: 0, maxMs: 1_000_000);
			buffer.StartRecording();
			using Barrier barrier = new(2);

			Task appender = Task.Run(
				() =>
				{
					barrier.SignalAndWait(cancellation);
					for (int i = 0; i < framesPerIteration; i++)
					{
						Append(buffer, frame);
					}
				},
				cancellation);

			barrier.SignalAndWait(cancellation);
			AudioClip clip = buffer.StopRecording();
			await appender;

			(clip.Samples.Count % frameSize).Should().Be(
				0, "a frame lands wholly before or wholly after the stop, never torn across it");
			clip.Samples.Should().OnlyContain(
				sample => sample == 1f, "a torn snapshot would surface default-valued samples");
		}
	}

	[Fact]
	public void Normalizes_device_frames_to_the_target_rate_while_recording()
	{
		CaptureBuffer buffer = NewBuffer(prerollMs: 0, maxMs: 1_000);

		buffer.StartRecording();
		// 4 samples at twice the target rate -> 2 samples retained.
		buffer.Append([0f, 0f, 0f, 0f], new CaptureFormat(2 * Rate, 1, 32, AudioSampleFormat.IeeeFloat));
		AudioClip clip = buffer.StopRecording();

		clip.SampleRate.Should().Be(Rate);
		clip.Samples.Should().HaveCount(2);
	}
}
