// Inner TDD loop for SilenceTrimmer: trailing near-silent samples are dropped; audio that ends in
// speech is left intact; an all-silent clip trims to empty.

using AwesomeAssertions;
using Domain.Audio;
using Xunit;

namespace Logic.AudioManagement.Tests;

public sealed class SilenceTrimmerTests
{
	private readonly SilenceTrimmer _trimmer = new();

	[Fact]
	public void Trims_trailing_silence()
	{
		AudioClip clip = new([0.5f, 0.4f, 0.0f, 0.0f], 16_000);

		AudioClip result = _trimmer.Trim(clip);

		result.Samples.Should().Equal(0.5f, 0.4f);
	}

	[Fact]
	public void Leaves_a_clip_that_ends_in_speech_unchanged()
	{
		AudioClip clip = new([0.5f, 0.4f], 16_000);

		_trimmer.Trim(clip).Samples.Should().Equal(0.5f, 0.4f);
	}

	[Fact]
	public void Reduces_an_all_silent_clip_to_empty()
	{
		AudioClip clip = new([0.0f, 0.0f, 0.0f], 16_000);

		_trimmer.Trim(clip).Samples.Should().BeEmpty();
	}
}
