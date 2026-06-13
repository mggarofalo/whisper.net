// Unit depth for the default command matcher: it must recognize nothing, whatever the
// transcript, so the command-mode hook stays inert and normal dictation is unchanged until a real
// matcher is implemented.

using AwesomeAssertions;
using Logic.AppManagement;
using Xunit;

namespace Logic.AppManagement.Tests;

public sealed class NoOpCommandMatcherTests
{
	private readonly NoOpCommandMatcher _matcher = new();

	[Theory]
	[InlineData("")]
	[InlineData("open settings")]
	[InlineData("schedule the meeting for friday")]
	public void Recognizes_no_command(string transcript)
	{
		Application.Commands.CommandMatch match = _matcher.Match(transcript);

		match.IsMatch.Should().BeFalse();
		match.Command.Should().BeNull();
	}
}
