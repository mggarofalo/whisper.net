// The Driver owns HOW UIPI-aware delivery is exercised (@WHISPER-6). It drives the REAL delivery
// pipeline through IMediator with the foreground-integrity probe faked, and asserts at the boundary
// that an elevated-window delivery is surfaced as a UIPI block (not silently dropped, no exception),
// while a same-integrity delivery types normally with no warning.

using Application.Ports;
using Application.Transcription;
using AwesomeAssertions;
using Dictation.Specs.Support;
using Domain.Audio;
using Mediator;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class UipiDeliveryDriver(
	IMediator mediator,
	IForegroundIntegrityProbe integrityProbe,
	ITextInjector textInjector,
	ScenarioWorld world)
{
	public void ForegroundWindowIntegrityIs(ForegroundIntegrity integrity) =>
		integrityProbe.CompareForegroundToCurrent().Returns(integrity);

	public async Task AttemptDelivery() =>
		world.LastResult = await mediator.Send(new DeliverTranscriptionCommand(world.CapturedClip));

	public void AssertBlockedByUipi()
	{
		world.LastResult.Should().NotBeNull();
		world.LastResult!.Block.Should().Be(DeliveryBlock.Uipi);
		world.LastResult.Delivered.Should().BeFalse();
		textInjector.DidNotReceive().Inject(Arg.Any<string>());
	}

	// Reaching an assertion at all means AttemptDelivery returned a result rather than throwing.
	public void AssertCompletedWithoutException() => world.LastResult.Should().NotBeNull();

	public void AssertDeliveredWithoutWarning(string expected)
	{
		world.LastResult.Should().NotBeNull();
		world.LastResult!.Delivered.Should().BeTrue();
		world.LastResult.Block.Should().Be(DeliveryBlock.None);
		textInjector.Received(1).Inject(expected);
	}
}
