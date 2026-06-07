// The Driver owns HOW a push-to-talk delivery is exercised, so step definitions stay one-liners that
// only describe WHAT. It configures the faked transcriber, sends the real DeliverTranscriptionCommand
// through IMediator, and asserts at the port boundary (text injected, or not).

using Application.Ports;
using Application.Transcription;
using Dictation.Specs.Support;
using Domain.Audio;
using Mediator;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class TranscriptionDriver(
	IMediator mediator,
	ITranscriber transcriber,
	ITextInjector textInjector,
	ScenarioWorld world)
{
	public void ModelWillTranscribeTo(string text) =>
		transcriber
			.TranscribeAsync(Arg.Any<AudioClip>(), Arg.Any<CancellationToken>())
			.Returns(new TranscriptionResult(text));

	public async Task ReleasePushToTalk() =>
		world.LastResult = await mediator.Send(new DeliverTranscriptionCommand(world.CapturedClip));

	public void AssertDelivered(string expected) =>
		textInjector.Received(1).Inject(expected);

	public void AssertNothingDelivered() =>
		textInjector.DidNotReceive().Inject(Arg.Any<string>());
}
