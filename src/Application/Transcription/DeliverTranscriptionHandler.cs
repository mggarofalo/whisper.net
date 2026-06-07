// Orchestrates the push-to-talk delivery pipeline: trim trailing silence (Logic), transcribe the
// clip (Infrastructure port), clean disfluencies (Logic), check the focused window is reachable, choose
// the delivery strategy (Logic), and inject the result into the focused field (Infrastructure port).
// Pure orchestration — every step's behavior lives behind a port, which is what lets the BDD specs
// drive this for real while faking only the Infrastructure boundary.

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
	IFillerWordCleaner fillerWordCleaner,
	IForegroundIntegrityProbe integrityProbe,
	IDeliveryStrategySelector strategySelector,
	IOptions<DeliveryOptions> deliveryOptions,
	ITextInjectorFactory textInjectorFactory)
	: ICommandHandler<DeliverTranscriptionCommand, DeliveryResult>
{
	public async ValueTask<DeliveryResult> Handle(DeliverTranscriptionCommand command, CancellationToken cancellationToken)
	{
		Domain.Audio.AudioClip trimmed = silenceTrimmer.Trim(command.Clip);
		Domain.Audio.TranscriptionResult transcription = await transcriber.TranscribeAsync(trimmed, cancellationToken);
		string cleaned = fillerWordCleaner.Clean(transcription.Text);

		// No speech (or only filler) -> deliver nothing.
		if (string.IsNullOrWhiteSpace(cleaned))
		{
			return new DeliveryResult(Delivered: false, Text: string.Empty);
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
