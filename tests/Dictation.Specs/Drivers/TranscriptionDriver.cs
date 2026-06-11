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
	FakeTextInjectorFactory injectors,
	ScenarioWorld world)
{
	public void ModelWillTranscribeTo(string text) =>
		transcriber
			.TranscribeAsync(Arg.Any<AudioClip>(), Arg.Any<CancellationToken>())
			.Returns(new TranscriptionResult(text));

	// WHISPER-125: the captured audio is silence, so the trimmer collapses it to empty and the pipeline must
	// not transcribe it (Whisper hallucinates a phrase on silence).
	public void CapturedAudioIsSilent() => world.CapturedClip = AudioClip.OneSecondOfSilence();

	public async Task ReleasePushToTalk() =>
		world.LastResult = await mediator.Send(new DeliverTranscriptionCommand(world.CapturedClip));

	// The default delivery strategy is Type, so a delivered phrase goes through the typing injector.
	public void AssertDelivered(string expected) =>
		injectors.Typing.Received(1).Inject(expected);

	public void AssertNothingDelivered() =>
		injectors.Typing.DidNotReceive().Inject(Arg.Any<string>());

	public void AssertNotTranscribed() =>
		transcriber.DidNotReceive().TranscribeAsync(Arg.Any<AudioClip>(), Arg.Any<CancellationToken>());
}
