// Drives the command-mode hook scenarios. It owns HOW the hook is exercised so the steps
// stay one-liners: it configures the faked command matcher (match or no-match), sends the REAL
// DeliverTranscriptionCommand through IMediator, and asserts at the boundary — a matched transcript is
// routed to the command branch (reported on the result) and never typed, while an unmatched transcript
// falls through to normal text delivery.

using Application.Commands;
using Application.Ports;
using Application.Transcription;
using AwesomeAssertions;
using Dictation.Specs.Support;
using Domain.Audio;
using Mediator;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class CommandModeDriver(
	IMediator mediator,
	ICommandMatcher matcher,
	ITranscriber transcriber,
	FakeTextInjectorFactory injectors,
	ScenarioWorld world)
{
	private const string Utterance = "open settings";

	public void MatcherRecognizesCommand() =>
		matcher.Match(Arg.Any<string>()).Returns(CommandMatch.For(Utterance));

	public void MatcherRecognizesNoCommand() =>
		matcher.Match(Arg.Any<string>()).Returns(CommandMatch.None);

	public async Task TranscribeUtterance()
	{
		transcriber
			.TranscribeAsync(Arg.Any<AudioClip>(), Arg.Any<CancellationToken>())
			.Returns(new TranscriptionResult(Utterance));

		world.LastResult = await mediator.Send(new DeliverTranscriptionCommand(world.CapturedClip));
	}

	// --- assertions ---

	public void AssertCommandBranchInvoked() => world.LastResult!.MatchedCommand.Should().Be(Utterance);

	public void AssertNotDeliveredAsText() => injectors.Typing.DidNotReceive().Inject(Arg.Any<string>());

	public void AssertDeliveredAsText() => injectors.Typing.Received(1).Inject(Utterance);
}
