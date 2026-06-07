// The command issued when push-to-talk is released: transcribe the captured clip and deliver the
// resulting text to the focused field. DeliveryResult reports whether anything was delivered, what the
// final text was, and — when nothing was delivered — why, so the UI can surface a blocked delivery
// instead of it appearing to silently do nothing.

using Application.Interfaces;
using Domain.Audio;

namespace Application.Transcription;

public sealed record DeliverTranscriptionCommand(AudioClip Clip) : ICommand<DeliveryResult>;

/// <summary>Why a delivery did not place text into the focused field.</summary>
public enum DeliveryBlock
{
	/// <summary>Delivery was not blocked (it either succeeded or there was no speech to deliver).</summary>
	None,

	/// <summary>
	/// The focused window is a higher-integrity (e.g. elevated) process; Windows UIPI would silently
	/// drop the synthetic input, so delivery was withheld and surfaced instead.
	/// </summary>
	Uipi,
}

public sealed record DeliveryResult(bool Delivered, string Text, DeliveryBlock Block = DeliveryBlock.None);
