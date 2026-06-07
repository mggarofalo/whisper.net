// The Driver owns HOW delivery-strategy selection is exercised (@WHISPER-8). It sets the scenario's
// configured default on the scoped DeliveryOptions and an optional per-delivery override, runs the REAL
// pipeline through IMediator, and asserts which delivery path was taken by checking which faked injector
// received the text. The transcriber is primed via the shared "model will transcribe" step.

using Application.Configuration;
using Application.Transcription;
using Dictation.Specs.Support;
using Domain.Settings;
using Mediator;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class DeliveryStrategyDriver(
	IMediator mediator,
	DeliveryOptions options,
	FakeTextInjectorFactory injectors,
	ScenarioWorld world)
{
	private DeliveryStrategy? _override;

	public void ConfiguredDefaultIs(DeliveryStrategy strategy) => options.DefaultStrategy = strategy;

	public void OverrideWith(DeliveryStrategy strategy) => _override = strategy;

	public async Task Deliver() =>
		world.LastResult = await mediator.Send(new DeliverTranscriptionCommand(world.CapturedClip, _override));

	public void AssertPathUsed(DeliveryStrategy expected)
	{
		if (expected == DeliveryStrategy.Paste)
		{
			injectors.Paste.Received(1).Inject(Arg.Any<string>());
			injectors.Typing.DidNotReceive().Inject(Arg.Any<string>());
		}
		else
		{
			injectors.Typing.Received(1).Inject(Arg.Any<string>());
			injectors.Paste.DidNotReceive().Inject(Arg.Any<string>());
		}
	}
}
