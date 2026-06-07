// Inner TDD loop for FillerWordCleaner: disfluencies are removed, ordinary text is preserved exactly,
// and blank input yields empty output.

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
}
