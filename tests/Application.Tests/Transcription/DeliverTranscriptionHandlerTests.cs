// Unit coverage for the delivery orchestration's branching: deliver normally, deliver nothing when
// there is no speech, route a matched transcript to the command branch instead of typing it
// (WHISPER-35), withhold and surface a UIPI block for a higher-integrity window (WHISPER-6), and route
// the delivery through the strategy the selector resolves (WHISPER-8). The Logic and Infrastructure
// ports are substituted so this pins the handler's decisions, not their implementations.

using Application.Commands;
using Application.Configuration;
using Application.Delivery;
using Application.Ports;
using Application.Transcription;
using Domain.Audio;
using Domain.Settings;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Application.Tests.Transcription;

public sealed class DeliverTranscriptionHandlerTests
{
	private readonly ISilenceTrimmer _silenceTrimmer = Substitute.For<ISilenceTrimmer>();
	private readonly ITranscriber _transcriber = Substitute.For<ITranscriber>();
	private readonly IPostProcessor _postProcessor = Substitute.For<IPostProcessor>();
	private readonly ICommandMatcher _commandMatcher = Substitute.For<ICommandMatcher>();
	private readonly IForegroundIntegrityProbe _integrityProbe = Substitute.For<IForegroundIntegrityProbe>();
	private readonly IDeliveryStrategySelector _strategySelector = Substitute.For<IDeliveryStrategySelector>();
	private readonly ITextInjectorFactory _textInjectorFactory = Substitute.For<ITextInjectorFactory>();
	private readonly ITextInjector _typingInjector = Substitute.For<ITextInjector>();
	private readonly ITextInjector _pasteInjector = Substitute.For<ITextInjector>();
	private readonly DeliveryOptions _deliveryOptions = new();

	public DeliverTranscriptionHandlerTests()
	{
		_textInjectorFactory.For(DeliveryStrategy.Type).Returns(_typingInjector);
		_textInjectorFactory.For(DeliveryStrategy.Paste).Returns(_pasteInjector);
		// Default: pass the resolved strategy straight through (override wins over the configured default).
		_strategySelector.Resolve(Arg.Any<DeliveryStrategy>(), Arg.Any<DeliveryStrategy?>())
			.Returns(ci => ci.ArgAt<DeliveryStrategy?>(1) ?? ci.ArgAt<DeliveryStrategy>(0));
		// Default: no command matches, so transcripts fall through to normal delivery.
		_commandMatcher.Match(Arg.Any<string>()).Returns(CommandMatch.None);
	}

	private DeliverTranscriptionHandler CreateHandler() =>
		new(_silenceTrimmer, _transcriber, _postProcessor, _commandMatcher, _integrityProbe, _strategySelector,
			Options.Create(_deliveryOptions), _textInjectorFactory);

	private void ModelTranscribesTo(string text)
	{
		_silenceTrimmer.Trim(Arg.Any<AudioClip>()).Returns(ci => ci.Arg<AudioClip>());
		_postProcessor.ProcessAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(ci => new ValueTask<string>(ci.Arg<string>()));
		_transcriber.TranscribeAsync(Arg.Any<AudioClip>(), Arg.Any<CancellationToken>())
			.Returns(new TranscriptionResult(text));
	}

	private async Task<DeliveryResult> Deliver(DeliveryStrategy? overrideStrategy = null) =>
		await CreateHandler().Handle(
			new DeliverTranscriptionCommand(AudioClip.OneSecondOfSilence(), overrideStrategy), CancellationToken.None);

	[Fact]
	public async Task Delivers_into_a_same_integrity_window_via_the_default_strategy()
	{
		ModelTranscribesTo("ship it");
		_integrityProbe.CompareForegroundToCurrent().Returns(ForegroundIntegrity.Same);

		DeliveryResult result = await Deliver();

		Assert.True(result.Delivered);
		Assert.Equal(DeliveryBlock.None, result.Block);
		_typingInjector.Received(1).Inject("ship it");
		_pasteInjector.DidNotReceive().Inject(Arg.Any<string>());
	}

	[Fact]
	public async Task Routes_to_the_paste_injector_when_the_resolved_strategy_is_paste()
	{
		ModelTranscribesTo("ship it");

		await Deliver(overrideStrategy: DeliveryStrategy.Paste);

		_pasteInjector.Received(1).Inject("ship it");
		_typingInjector.DidNotReceive().Inject(Arg.Any<string>());
	}

	[Fact]
	public async Task Withholds_and_surfaces_a_uipi_block_for_a_higher_integrity_window()
	{
		ModelTranscribesTo("ship it");
		_integrityProbe.CompareForegroundToCurrent().Returns(ForegroundIntegrity.Higher);

		DeliveryResult result = await Deliver();

		Assert.False(result.Delivered);
		Assert.Equal(DeliveryBlock.Uipi, result.Block);
		_typingInjector.DidNotReceive().Inject(Arg.Any<string>());
		_pasteInjector.DidNotReceive().Inject(Arg.Any<string>());
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
		_typingInjector.Received(1).Inject("ship it");
	}

	[Fact]
	public async Task Routes_a_matched_transcript_to_the_command_branch_instead_of_typing_it()
	{
		ModelTranscribesTo("open settings");
		_commandMatcher.Match("open settings").Returns(CommandMatch.For("open settings"));

		DeliveryResult result = await Deliver();

		Assert.False(result.Delivered);
		Assert.Equal("open settings", result.MatchedCommand);
		_typingInjector.DidNotReceive().Inject(Arg.Any<string>());
		_pasteInjector.DidNotReceive().Inject(Arg.Any<string>());
		// The command branch supersedes delivery, so the focused-window integrity is never probed.
		_integrityProbe.DidNotReceive().CompareForegroundToCurrent();
	}

	[Fact]
	public async Task Delivers_nothing_and_does_not_probe_when_there_is_no_speech()
	{
		ModelTranscribesTo("   ");

		DeliveryResult result = await Deliver();

		Assert.False(result.Delivered);
		Assert.Equal(DeliveryBlock.None, result.Block);
		_typingInjector.DidNotReceive().Inject(Arg.Any<string>());
		_pasteInjector.DidNotReceive().Inject(Arg.Any<string>());
		_integrityProbe.DidNotReceive().CompareForegroundToCurrent();
	}

	[Fact]
	public async Task Skips_transcription_entirely_when_the_clip_has_no_speech()
	{
		// The trimmer collapses an all-silence clip to empty (WHISPER-112); feeding that to Whisper makes it
		// hallucinate a phrase (WHISPER-125), so the handler must not call the transcriber at all.
		_silenceTrimmer.Trim(Arg.Any<AudioClip>()).Returns(AudioClip.OneSecondOfSilence() with { Samples = [] });

		DeliveryResult result = await Deliver();

		Assert.False(result.Delivered);
		Assert.Equal(DeliveryBlock.None, result.Block);
		await _transcriber.DidNotReceive().TranscribeAsync(Arg.Any<AudioClip>(), Arg.Any<CancellationToken>());
		_typingInjector.DidNotReceive().Inject(Arg.Any<string>());
		_integrityProbe.DidNotReceive().CompareForegroundToCurrent();
	}
}
