// The visual state the dictation overlay communicates (WHISPER-102), so the user can tell at a glance
// what the app is doing. Distinct from the coarse Domain.Recording.RecordingState: this is the overlay's
// own presentation state, including an Error state the recording machine has no concept of. The overlay
// is hidden at rest, so there is no Idle member here — these are only the states it shows.

namespace Logic.AppManagement;

public enum OverlayState
{
	/// <summary>The microphone is live and being captured.</summary>
	Recording,

	/// <summary>Capture has stopped; the clip is being transcribed and delivered.</summary>
	Transcribing,

	/// <summary>The last dictation failed (capture or pipeline error); shown briefly, then dismissed.</summary>
	Error,

	/// <summary>The dictation model is warming up (WHISPER-129); shown while the warm-up runs, until it
	/// clears. A background state — any active recording/transcribing/error takes precedence over it.</summary>
	Warming,
}
