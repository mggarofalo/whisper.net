// Port for non-visual audio feedback at dictation pipeline transitions. Implemented in
// Infrastructure by an NAudio player; faked in the specs so the orchestrator's firing can be asserted
// without a real output device. Playback is fire-and-forget: Play returns immediately and never throws,
// so feedback can never block or fail the dictation flow. A playback failure (e.g. no output device) is
// the implementation's concern to log and swallow.

using Domain.Feedback;

namespace Application.Ports;

public interface IAudioFeedback
{
	/// <summary>Plays the cue for <paramref name="sound"/>. Returns immediately; never throws.</summary>
	void Play(FeedbackSound sound);
}
