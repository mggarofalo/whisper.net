// Published on the shared IMessenger when a dictation's recording held no audio at all (silent
// throughout — usually a muted microphone), so it was never transcribed and nothing was typed. The
// overlay shows a brief "No audio detected" message instead of just vanishing (WHISPER-144); it replaces
// the tray balloon WHISPER-141 used, which Windows can suppress (Focus Assist, notification settings).
// Carries nothing — the overlay's wording is its own.

namespace Application.Dictation;

public sealed record DictationNoAudioMessage;
