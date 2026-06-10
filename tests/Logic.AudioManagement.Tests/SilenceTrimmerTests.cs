// Inner TDD loop for SilenceTrimmer (WHISPER-112): only a SUSTAINED sub-threshold tail counts as dead
// air — a shorter quiet tail is the soft end of speech and is preserved; a trimmed tail keeps a short
// pad of the actually-recorded samples beyond the last speech; a clip that ends in speech is unchanged;
// an all-silent clip trims to empty. (The pre-112 per-sample backward walk trimmed quiet word endings —
// the "Trims_trailing_silence" pin was updated to a sustained tail when that behavior changed.)

using AwesomeAssertions;
using Domain.Audio;
using Xunit;

namespace Logic.AudioManagement.Tests;

public sealed class SilenceTrimmerTests
{
	private const int SampleRate = 16_000;

	// Defaults: 150 ms silence window (2400 samples), 50 ms pad (800 samples), 0.01 amplitude threshold.
	private readonly SilenceTrimmerOptions _options = new();
	private readonly SilenceTrimmer _trimmer;

	public SilenceTrimmerTests() => _trimmer = new SilenceTrimmer(_options);

	private int WindowSamples => _options.TrailingSilenceWindowMs * SampleRate / 1000;

	private int PadSamples => _options.TrailingPadMs * SampleRate / 1000;

	[Fact]
	public void Trims_a_sustained_trailing_silence_down_to_the_pad()
	{
		// 100 ms of speech then 300 ms of dead air — twice the 150 ms window.
		AudioClip clip = new(Samples((0.5f, 1600), (0f, 4800)), SampleRate);

		AudioClip result = _trimmer.Trim(clip);

		result.Samples.Should().HaveCount(1600 + PadSamples);
		result.Samples.Take(1600).Should().OnlyContain(sample => sample == 0.5f);
		result.Samples.Skip(1600).Should().OnlyContain(sample => sample == 0f);
	}

	[Fact]
	public void Preserves_a_quiet_tail_shorter_than_the_silence_window()
	{
		// 100 ms of sub-threshold tail — too short to be dead air: the soft end of speech survives.
		AudioClip clip = new(Samples((0.5f, 1600), (0.005f, 1600)), SampleRate);

		_trimmer.Trim(clip).Samples.Should().Equal(clip.Samples);
	}

	[Fact]
	public void The_pad_keeps_the_recorded_samples_not_synthesized_silence()
	{
		// The sustained quiet tail is real recorded audio; the kept pad must be those samples.
		AudioClip clip = new(Samples((0.5f, 1600), (0.005f, 4800)), SampleRate);

		AudioClip result = _trimmer.Trim(clip);

		result.Samples.Should().HaveCount(1600 + PadSamples);
		result.Samples.Skip(1600).Should().OnlyContain(sample => sample == 0.005f);
	}

	[Fact]
	public void A_tail_exactly_at_the_window_is_trimmed()
	{
		AudioClip clip = new(Samples((0.5f, 1600), (0f, WindowSamples)), SampleRate);

		_trimmer.Trim(clip).Samples.Should().HaveCount(1600 + PadSamples);
	}

	[Fact]
	public void A_tail_just_under_the_window_is_preserved()
	{
		AudioClip clip = new(Samples((0.5f, 1600), (0f, WindowSamples - 1)), SampleRate);

		_trimmer.Trim(clip).Samples.Should().Equal(clip.Samples);
	}

	[Fact]
	public void Leaves_a_clip_that_ends_in_speech_unchanged()
	{
		AudioClip clip = new([0.5f, 0.4f], SampleRate);

		_trimmer.Trim(clip).Samples.Should().Equal(0.5f, 0.4f);
	}

	[Fact]
	public void Reduces_an_all_silent_clip_to_empty()
	{
		AudioClip clip = new([0.0f, 0.0f, 0.0f], SampleRate);

		_trimmer.Trim(clip).Samples.Should().BeEmpty();
	}

	// Concatenated runs of (amplitude, count) — speech followed by a tail, in clip order.
	private static float[] Samples(params (float Amplitude, int Count)[] runs)
	{
		float[] samples = new float[runs.Sum(run => run.Count)];
		int offset = 0;
		foreach ((float amplitude, int count) in runs)
		{
			Array.Fill(samples, amplitude, offset, count);
			offset += count;
		}

		return samples;
	}
}
