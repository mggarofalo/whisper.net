// Inner TDD loop for SilenceTrimmer: end-of-speech is detected by ENERGY (RMS per frame),
// not raw per-sample amplitude, so quiet trailing speech (real frame energy, low per-sample values) is
// preserved even when SUSTAINED, while only genuine near-silence (RMS below the threshold) is trimmed —
// and then only when sustained past the window. A trimmed tail keeps a short pad of the
// actually-recorded samples; a clip ending in speech is unchanged; an all-silent clip trims to empty. The
// energy boundary is pinned strictly: a frame whose RMS is BELOW the threshold is silence, so a tail at
// exactly the threshold — or a loud negative one — is speech. (The reopen fix: a per-sample 0.01f bar cut
// low-energy word endings; the energy-aware detection keeps a sustained 0.005 tail as speech.)

using AwesomeAssertions;
using Domain.Audio;
using Xunit;

namespace Logic.AudioManagement.Tests;

public sealed class SilenceTrimmerTests
{
	private const int SampleRate = 16_000;

	// Defaults: 150 ms silence window (2400 samples), 50 ms pad (800 samples), 0.002 RMS energy threshold,
	// 20 ms frames (320 samples).
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
	public void Preserves_sustained_quiet_trailing_speech()
	{
		// The reopen fix: 300 ms (twice the window) of quiet 0.005 trailing speech. Its per-sample values
		// sit below the old 0.01 bar, but its frame RMS (0.005) is above the energy threshold (0.002), so
		// it is speech and survives — where the per-sample trimmer cut it as dead air.
		AudioClip clip = new(Samples((0.5f, 1600), (0.005f, 4800)), SampleRate);

		_trimmer.Trim(clip).Samples.Should().Equal(clip.Samples);
	}

	[Fact]
	public void The_pad_keeps_the_recorded_samples_not_synthesized_silence()
	{
		// A sustained sub-threshold (0.001 RMS, below the 0.002 energy floor) tail is genuine near-silence
		// and is trimmed; the kept pad must be those real recorded samples, not synthesized zeroes.
		AudioClip clip = new(Samples((0.5f, 1600), (0.001f, 4800)), SampleRate);

		AudioClip result = _trimmer.Trim(clip);

		result.Samples.Should().HaveCount(1600 + PadSamples);
		result.Samples.Skip(1600).Should().OnlyContain(sample => sample == 0.001f);
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
	public void A_tail_above_the_energy_floor_counts_as_speech()
	{
		// A sustained tail whose frame energy is above the floor is speech, so even at twice the window it
		// survives untrimmed. 0.0035 sits above the 0.002 threshold (and below the old 0.01 per-sample bar
		// that wrongly cut it).
		AudioClip clip = new(Samples((0.5f, 1600), (0.0035f, WindowSamples * 2)), SampleRate);

		_trimmer.Trim(clip).Samples.Should().Equal(clip.Samples);
	}

	[Fact]
	public void Sustained_near_silence_below_the_energy_floor_is_trimmed()
	{
		// Genuine dead air at the noise floor (0.001 RMS, below the 0.002 threshold), sustained past the
		// window, is trimmed — the counterpart to the preserved quiet-speech tail.
		AudioClip clip = new(Samples((0.5f, 1600), (0.001f, WindowSamples * 2)), SampleRate);

		_trimmer.Trim(clip).Samples.Should().HaveCount(1600 + PadSamples);
	}

	[Fact]
	public void Loud_negative_samples_count_as_speech()
	{
		// Real audio is bipolar: silence is judged on rectified amplitude, so a tail of loud
		// negative-half-wave samples is speech, never dead air.
		AudioClip clip = new(Samples((0.5f, 1600), (-0.5f, WindowSamples * 2)), SampleRate);

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
