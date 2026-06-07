// Orchestrates the push-to-talk delivery pipeline: trim trailing silence (Logic), transcribe the
// clip (Infrastructure port), clean disfluencies (Logic), check the focused window is reachable, and
// inject the result into the focused field (Infrastructure port). Pure orchestration — every step's
// behavior lives behind a port, which is what lets the BDD specs drive this for real while faking only
// the Infrastructure boundary.

using Application.Interfaces;
using Application.Ports;

namespace Application.Transcription;

public sealed class DeliverTranscriptionHandler(
	ISilenceTrimmer silenceTrimmer,
	ITranscriber transcriber,
	IFillerWordCleaner fillerWordCleaner,
	IForegroundIntegrityProbe integrityProbe,
	ITextInjector textInjector)
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

		textInjector.Inject(cleaned);
		return new DeliveryResult(Delivered: true, Text: cleaned);
	}
}
