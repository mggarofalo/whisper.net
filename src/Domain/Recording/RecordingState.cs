// The authoritative state of a dictation capture. A single state machine (Logic.AppManagement) owns
// the legal transitions Idle -> Recording -> Transcribing -> Idle, plus an Esc cancel back to Idle, so
// the pipeline can never overlap captures or deliver a cancelled result. Lives in Domain because the
// state machine, the tray/UI that reflects status, and the orchestration that drives it all speak
// these three words.

namespace Domain.Recording;

public enum RecordingState
{
	/// <summary>Nothing is being captured; the resting state.</summary>
	Idle,

	/// <summary>The microphone is being captured.</summary>
	Recording,

	/// <summary>Capture has stopped and the audio is being transcribed.</summary>
	Transcribing,
}
