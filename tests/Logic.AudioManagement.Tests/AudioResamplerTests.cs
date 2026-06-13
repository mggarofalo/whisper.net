// Inner TDD loop for AudioResampler: channel downmixing averages, same-rate is identity, and
// resampling scales the sample count by the rate ratio. Edge cases (empty, zero channels) are the
// depth behind the normalization outline.

using AwesomeAssertions;
using Domain.Audio;
using Xunit;

namespace Logic.AudioManagement.Tests;

public sealed class AudioResamplerTests
{
	private readonly AudioResampler _resampler = new();

	private static CaptureFormat Format(int rate, int channels) => new(rate, channels, 32, AudioSampleFormat.IeeeFloat);

	[Fact]
	public void Downmixes_stereo_to_mono_by_averaging_channels()
	{
		// Interleaved L/R: (1,3) -> 2, (2,4) -> 3.
		float[] stereo = [1f, 3f, 2f, 4f];

		float[] mono = _resampler.ToMono(stereo, Format(16_000, 2), 16_000);

		mono.Should().Equal(2f, 3f);
	}

	[Fact]
	public void Same_rate_mono_is_identity()
	{
		float[] samples = [0.1f, -0.2f, 0.3f];

		_resampler.ToMono(samples, Format(16_000, 1), 16_000).Should().Equal(0.1f, -0.2f, 0.3f);
	}

	[Fact]
	public void Resampling_scales_the_sample_count_by_the_rate_ratio()
	{
		// 48 mono samples at 48 kHz -> 16 samples at 16 kHz.
		float[] samples = new float[48];

		_resampler.ToMono(samples, Format(48_000, 1), 16_000).Should().HaveCount(16);
	}

	[Fact]
	public void Halving_the_rate_halves_the_sample_count()
	{
		float[] samples = new float[4];

		_resampler.ToMono(samples, Format(32_000, 1), 16_000).Should().HaveCount(2);
	}

	[Fact]
	public void Empty_input_yields_empty_output()
	{
		_resampler.ToMono([], Format(44_100, 2), 16_000).Should().BeEmpty();
	}

	[Fact]
	public void Rejects_a_format_with_no_channels()
	{
		Action act = () => _resampler.ToMono([1f], Format(16_000, 0), 16_000);

		act.Should().Throw<ArgumentOutOfRangeException>();
	}
}
