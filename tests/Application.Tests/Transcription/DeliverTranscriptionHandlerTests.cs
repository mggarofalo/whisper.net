// Unit coverage for the delivery orchestration's branching (WHISPER-6 adds the UIPI branch). The Logic
// and Infrastructure ports are substituted so the test pins the handler's decisions: deliver normally,
// deliver nothing when there is no speech, and — the new behavior — withhold delivery and surface a
// UIPI block when the focused window is higher-integrity. Uncertainty must not block.

using Application.Ports;
using Application.Transcription;
using Domain.Audio;
using NSubstitute;
using Xunit;

namespace Application.Tests.Transcription;

public sealed class DeliverTranscriptionHandlerTests
{
	private readonly ISilenceTrimmer _silenceTrimmer = Substitute.For<ISilenceTrimmer>();
	private readonly ITranscriber _transcriber = Substitute.For<ITranscriber>();
	private readonly IFillerWordCleaner _fillerWordCleaner = Substitute.For<IFillerWordCleaner>();
	private readonly IForegroundIntegrityProbe _integrityProbe = Substitute.For<IForegroundIntegrityProbe>();
	private readonly ITextInjector _textInjector = Substitute.For<ITextInjector>();

	private DeliverTranscriptionHandler CreateHandler() =>
		new(_silenceTrimmer, _transcriber, _fillerWordCleaner, _integrityProbe, _textInjector);

	private void ModelTranscribesTo(string text)
	{
		_silenceTrimmer.Trim(Arg.Any<AudioClip>()).Returns(ci => ci.Arg<AudioClip>());
		_fillerWordCleaner.Clean(Arg.Any<string>()).Returns(ci => ci.Arg<string>());
		_transcriber.TranscribeAsync(Arg.Any<AudioClip>(), Arg.Any<CancellationToken>())
			.Returns(new TranscriptionResult(text));
	}

	private async Task<DeliveryResult> Deliver() =>
		await CreateHandler().Handle(new DeliverTranscriptionCommand(AudioClip.OneSecondOfSilence()), CancellationToken.None);

	[Fact]
	public async Task Delivers_into_a_same_integrity_window()
	{
		ModelTranscribesTo("ship it");
		_integrityProbe.CompareForegroundToCurrent().Returns(ForegroundIntegrity.Same);

		DeliveryResult result = await Deliver();

		Assert.True(result.Delivered);
		Assert.Equal(DeliveryBlock.None, result.Block);
		_textInjector.Received(1).Inject("ship it");
	}

	[Fact]
	public async Task Withholds_and_surfaces_a_uipi_block_for_a_higher_integrity_window()
	{
		ModelTranscribesTo("ship it");
		_integrityProbe.CompareForegroundToCurrent().Returns(ForegroundIntegrity.Higher);

		DeliveryResult result = await Deliver();

		Assert.False(result.Delivered);
		Assert.Equal(DeliveryBlock.Uipi, result.Block);
		_textInjector.DidNotReceive().Inject(Arg.Any<string>());
	}

	[Theory]
	[InlineData(ForegroundIntegrity.Unknown)]
	[InlineData(ForegroundIntegrity.Lower)]
	public async Task Does_not_block_when_the_window_is_not_higher_integrity(ForegroundIntegrity integrity)
	{
		ModelTranscribesTo("ship it");
		_integrityProbe.CompareForegroundToCurrent().Returns(integrity);

		DeliveryResult result = await Deliver();

		Assert.True(result.Delivered);
		_textInjector.Received(1).Inject("ship it");
	}

	[Fact]
	public async Task Delivers_nothing_and_does_not_probe_when_there_is_no_speech()
	{
		ModelTranscribesTo("   ");

		DeliveryResult result = await Deliver();

		Assert.False(result.Delivered);
		Assert.Equal(DeliveryBlock.None, result.Block);
		_textInjector.DidNotReceive().Inject(Arg.Any<string>());
		_integrityProbe.DidNotReceive().CompareForegroundToCurrent();
	}
}
