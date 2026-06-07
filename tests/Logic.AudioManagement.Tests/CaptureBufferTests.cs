// Inner TDD loop for CaptureBuffer: the preroll ring retains only the most recent samples, recording
// is seeded with that preroll, the max-duration cap trims and fires exactly once, and the buffer is
// reusable across recordings. Uses a 1 kHz target rate so 1 sample == 1 ms and the arithmetic is
// obvious.

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
	public void Cap_trims_the_recording_and_fires_once()
	{
		CaptureBuffer buffer = NewBuffer(prerollMs: 0, maxMs: 3);
		int fired = 0;
		buffer.MaxDurationReached += (_, _) => fired++;

		buffer.StartRecording();
		Append(buffer, 1f, 2f, 3f, 4f, 5f); // exceeds the 3-sample cap
		AudioClip clip = buffer.StopRecording();

		clip.Samples.Should().Equal(1f, 2f, 3f);
		fired.Should().Be(1);
	}

	[Fact]
	public void Appends_after_the_cap_are_ignored()
	{
		CaptureBuffer buffer = NewBuffer(prerollMs: 0, maxMs: 3);

		buffer.StartRecording();
		Append(buffer, 1f, 2f, 3f);
		Append(buffer, 4f, 5f); // already capped
		AudioClip clip = buffer.StopRecording();

		clip.Samples.Should().Equal(1f, 2f, 3f);
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
