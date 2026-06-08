// The payload of a dictation pipeline-stage transition: where the orchestrator was and where it now is.
// Raised on every accepted stage change so the UI/overlay and diagnostics can observe pipeline progress
// (WHISPER-14, consumed by the level overlay in WHISPER-26).

namespace Logic.AppManagement;

public sealed record DictationStageChangedEventArgs(DictationStage Previous, DictationStage Current);
