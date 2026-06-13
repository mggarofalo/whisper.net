// The non-visual cues the dictation pipeline can play so the user hears where they are without looking
//: a sound when recording starts, when it stops, and when transcription completes. Lives
// in Domain because the orchestrator that fires these and the Infrastructure player that renders them
// both speak these three cues.

namespace Domain.Feedback;

public enum FeedbackSound
{
	/// <summary>Recording has begun.</summary>
	RecordingStarted,

	/// <summary>Recording has stopped and the audio is being transcribed.</summary>
	RecordingStopped,

	/// <summary>Transcription finished and the recognized text is ready.</summary>
	TranscriptionComplete,
}
