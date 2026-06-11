// Orchestrates the push-to-talk delivery pipeline: trim trailing silence (Logic), transcribe the
// clip (Infrastructure port), clean disfluencies (Logic), match the transcript against voice commands
// (the command-mode hook), check the focused window is reachable, choose the delivery strategy (Logic),
// and inject the result into the focused field (Infrastructure port). Pure orchestration — every step's
// behavior lives behind a port, which is what lets the BDD specs drive this for real while faking only
// the Infrastructure boundary.

using Application.Commands;
using Application.Configuration;
using Application.Delivery;
using Application.Interfaces;
using Application.Ports;
using Domain.Settings;
using Microsoft.Extensions.Options;

namespace Application.Transcription;

public sealed class DeliverTranscriptionHandler(
	ISilenceTrimmer silenceTrimmer,
	ITranscriber transcriber,
	IPostProcessor postProcessor,
	ICommandMatcher commandMatcher,
	IForegroundIntegrityProbe integrityProbe,
	IDeliveryStrategySelector strategySelector,
	IOptions<DeliveryOptions> deliveryOptions,
	ITextInjectorFactory textInjectorFactory)
	: ICommandHandler<DeliverTranscriptionCommand, DeliveryResult>
{
	public async ValueTask<DeliveryResult> Handle(DeliverTranscriptionCommand command, CancellationToken cancellationToken)
	{
		Domain.Audio.AudioClip trimmed = silenceTrimmer.Trim(command.Clip);

		// No speech: the trimmer collapses a clip that is sub-threshold throughout to empty (WHISPER-112).
		// Feeding empty/silent audio to Whisper makes it HALLUCINATE a phrase (WHISPER-125 — e.g. it emitted
		// "SILENT PRACTICE" on a first, near-silent dictation), so skip transcription and deliver nothing.
		// Quiet speech stays above the energy floor, so it is never collapsed and still transcribes.
		if (trimmed.Samples.Count == 0)
		{
			return new DeliveryResult(Delivered: false, Text: string.Empty);
		}

		Domain.Audio.TranscriptionResult transcription = await transcriber.TranscribeAsync(trimmed, cancellationToken);

		// Post-processing (WHISPER-41): normalize (filler/noise per config) then the optional output
		// transform, applied in a fixed order behind the IPostProcessor port.
		string cleaned = await postProcessor.ProcessAsync(transcription.Text, cancellationToken);

		// No speech (or only filler) -> deliver nothing.
		if (string.IsNullOrWhiteSpace(cleaned))
		{
			return new DeliveryResult(Delivered: false, Text: string.Empty);
		}

		// Command-mode hook (WHISPER-35): consult the matcher after transcription/clean-up and before
		// delivery. On a match the transcript is routed to the command branch instead of being typed —
		// execution is out of scope here (scaffolding only), so we report the matched command and deliver
		// no text. The default matcher never matches, so normal dictation is unchanged.
		CommandMatch match = commandMatcher.Match(cleaned);
		if (match.IsMatch)
		{
			return new DeliveryResult(Delivered: false, Text: cleaned, MatchedCommand: match.Command);
		}

		// UIPI: synthetic input from our (unelevated) process into a higher-integrity window is silently
		// dropped by Windows. Detect that and surface it as a blocked result rather than typing into the
		// void. Uncertainty (Unknown) does not block — attempting delivery is better than wrongly refusing.
		if (integrityProbe.CompareForegroundToCurrent() == ForegroundIntegrity.Higher)
		{
			return new DeliveryResult(Delivered: false, Text: cleaned, Block: DeliveryBlock.Uipi);
		}

		// Pick the strategy for this delivery — a per-delivery override wins, else the configured default —
		// and route to the matching injector without caring which mechanism (typing vs paste) backs it.
		DeliveryStrategy strategy = strategySelector.Resolve(deliveryOptions.Value.DefaultStrategy, command.StrategyOverride);
		textInjectorFactory.For(strategy).Inject(cleaned);
		return new DeliveryResult(Delivered: true, Text: cleaned);
	}
}
