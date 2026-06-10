// Published on the shared IMessenger when an in-progress recording reaches the HARD failsafe ceiling
// (WHISPER-111). Unlike the soft-limit signals, this one accompanies action: the orchestrator stops
// the dictation itself at this bound — through the normal stop path, so the recording is transcribed
// and delivered, never discarded. This signal exists so the UI can tell the user why dictation
// stopped on its own. Carries how much was recorded and the configured hard limit, in milliseconds.

namespace Application.Dictation;

public sealed record DictationHardLimitStopMessage(int RecordedMs, int LimitMs);
