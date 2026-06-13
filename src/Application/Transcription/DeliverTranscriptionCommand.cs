// The command issued when push-to-talk is released: transcribe the captured clip and deliver the
// resulting text to the focused field. DeliveryResult reports whether anything was delivered, what the
// final text was, and — when nothing was delivered — why, so the UI can surface a blocked delivery
// instead of it appearing to silently do nothing.

using Application.Interfaces;
using Domain.Audio;
using Domain.Settings;

namespace Application.Transcription;

// StrategyOverride forces a delivery strategy for this delivery only (e.g. the pipeline forcing paste
// for a very long result); when null the configured default strategy applies.
public sealed record DeliverTranscriptionCommand(AudioClip Clip, DeliveryStrategy? StrategyOverride = null)
	: ICommand<DeliveryResult>;

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

// MatchedCommand names the voice command a transcript was routed to: when set, the
// transcript was recognized as a command and routed to the command branch instead of being typed, so
// Delivered is false and Text holds the (undelivered) transcript. Null whenever no command matched.
public sealed record DeliveryResult(
	bool Delivered,
	string Text,
	DeliveryBlock Block = DeliveryBlock.None,
	string? MatchedCommand = null);
