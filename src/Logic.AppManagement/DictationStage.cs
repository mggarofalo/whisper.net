// The fine-grained pipeline stage owned by the DictationOrchestrator (WHISPER-14): the explicit states
// one utterance moves through end to end. Distinct from Domain.Recording.RecordingState (the coarse
// Idle/Recording/Transcribing the tray reflects) because it adds a Delivering stage, so the orchestrator
// can log and reason about the hand-off to text injection as its own step. The orchestrator keeps the
// shared RecordingStateMachine in step for the tray/UI.

namespace Logic.AppManagement;

public enum DictationStage
{
	/// <summary>Nothing is being captured or delivered; the resting state.</summary>
	Idle,

	/// <summary>The microphone is being captured.</summary>
	Recording,

	/// <summary>Capture has stopped and the audio is being transcribed.</summary>
	Transcribing,

	/// <summary>Transcription is complete and the recognized text is being delivered to the focused field.</summary>
	Delivering,
}
