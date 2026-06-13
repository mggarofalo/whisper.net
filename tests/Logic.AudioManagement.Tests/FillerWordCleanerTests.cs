// Inner TDD loop for FillerWordCleaner: noise labels are always stripped, fillers are
// removed only when enabled (with elongation and trailing-punctuation handling), whitespace is
// collapsed and trimmed, blank input yields empty output, and the function is idempotent.

using AwesomeAssertions;
using Xunit;

namespace Logic.AudioManagement.Tests;

public sealed class FillerWordCleanerTests
{
	private readonly FillerWordCleaner _cleaner = new();

	[Fact]
	public void Removes_filler_words()
	{
		_cleaner.Clean("um hello uh world").Should().Be("hello world");
	}

	[Fact]
	public void Preserves_text_that_has_no_fillers()
	{
		_cleaner.Clean("schedule the meeting for friday").Should().Be("schedule the meeting for friday");
	}

	[Fact]
	public void Returns_empty_for_blank_input()
	{
		_cleaner.Clean("   ").Should().BeEmpty();
	}

	[Theory]
	[InlineData("Hello [BLANK_AUDIO] world", "Hello world")]
	[InlineData("[SILENCE]", "")]
	[InlineData("keep [ S ] going", "keep going")]
	[InlineData("nice (music) here", "nice here")]
	public void Strips_bracketed_and_parenthesized_noise_labels(string raw, string expected)
	{
		_cleaner.Clean(raw).Should().Be(expected);
	}

	[Theory]
	[InlineData("Um, I think so", "I think so")]
	[InlineData("So uh we should go", "So we should go")]
	[InlineData("Hmm let me check", "let me check")]
	[InlineData("Ummm, okay then", "okay then")]
	[InlineData("erm right", "right")]
	[InlineData("mhm sure", "sure")]
	public void Removes_fillers_with_elongation_and_trailing_punctuation_when_enabled(string raw, string expected)
	{
		_cleaner.Clean(raw, removeFillerWords: true).Should().Be(expected);
	}

	[Fact]
	public void Keeps_fillers_when_removal_is_disabled_but_still_strips_noise_labels()
	{
		_cleaner.Clean("Um [SILENCE] keep this", removeFillerWords: false).Should().Be("Um keep this");
	}

	[Fact]
	public void Does_not_remove_fillers_embedded_in_real_words()
	{
		_cleaner.Clean("summer hammer error").Should().Be("summer hammer error");
	}

	[Theory]
	[InlineData("Um, I think so", true)]
	[InlineData("Hello [BLANK_AUDIO] world", true)]
	[InlineData("Um [SILENCE] keep this", false)]
	public void Is_idempotent(string raw, bool removeFillerWords)
	{
		string once = _cleaner.Clean(raw, removeFillerWords);

		_cleaner.Clean(once, removeFillerWords).Should().Be(once);
	}
}
