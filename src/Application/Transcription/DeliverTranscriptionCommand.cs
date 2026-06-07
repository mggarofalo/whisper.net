// The command issued when push-to-talk is released: transcribe the captured clip and deliver the
// resulting text to the focused field. DeliveryResult reports whether anything was delivered and
// what the final text was.

using Application.Interfaces;
using Domain.Audio;

namespace Application.Transcription;

public sealed record DeliverTranscriptionCommand(AudioClip Clip) : ICommand<DeliveryResult>;

public sealed record DeliveryResult(bool Delivered, string Text);
