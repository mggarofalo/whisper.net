// Inner TDD loop for CaptureBuffer: the preroll ring retains only the most recent samples, recording
// is seeded with that preroll, the soft max-duration limit (WHISPER-111) signals once at 80% and once
// at 100% while every sample — including those past the limit — is retained, and the buffer is
// reusable across recordings (limit signals re-arm per recording). Uses a 1 kHz target rate so
// 1 sample == 1 ms and the arithmetic is obvious.

using AwesomeAssertions;
using Domain.Audio;
using Xunit;

namespace Logic.AudioManagement.Tests;

public sealed class CaptureBufferTests
{
	private const int Rate = 1_000;

	private static CaptureBuffer NewBuffer(int prerollMs, int maxMs) =>
		new(new AudioBufferingOptions(prerollMs, maxMs, TargetSampleRate: Rate), new AudioResampler());

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
