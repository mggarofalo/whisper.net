// The single authoritative recording state machine: Idle -> Recording -> Transcribing -> Idle, with an
// Esc cancel from an in-flight capture back to Idle. It consumes activation requests (from the hotkey
// controller, wired in M7) and signals downstream audio/transcription work, keeping illegal
// transitions impossible — a request that doesn't fit the current state is a no-op, never an error
// state. Every change is observable so the tray/UI can reflect status, and a cancel raises a distinct
// signal so the in-flight capture is discarded and no text is ever delivered for it.

using Domain.Recording;

namespace Logic.AppManagement;

public sealed class RecordingStateMachine
{
	public RecordingState State { get; private set; } = RecordingState.Idle;

	/// <summary>Raised on every accepted transition, carrying the previous and current state.</summary>
	public event EventHandler<RecordingStateChangedEventArgs>? StateChanged;

	/// <summary>
	/// Raised when an in-flight capture is cancelled, so the orchestration discards the audio and
	/// delivers nothing. Distinct from a normal stop, which routes the capture into transcription.
	/// </summary>
	public event EventHandler? Cancelled;

	// Idle -> Recording. Ignored unless currently Idle.
	public void RequestStart() => Transition(RecordingState.Idle, RecordingState.Recording);

	// Recording -> Transcribing (the path that leads to delivery). Ignored unless currently Recording.
	public void RequestStop() => Transition(RecordingState.Recording, RecordingState.Transcribing);

	// Transcribing -> Idle. Ignored unless currently Transcribing.
	public void CompleteTranscription() => Transition(RecordingState.Transcribing, RecordingState.Idle);

	// Esc: discard an in-flight capture and return to Idle without transcribing or delivering. A cancel
	// from Idle is a no-op (nothing to discard).
	public void Cancel()
	{
		if (State is RecordingState.Recording or RecordingState.Transcribing)
		{
			Cancelled?.Invoke(this, EventArgs.Empty);
			MoveTo(RecordingState.Idle);
		}
	}

	private void Transition(RecordingState from, RecordingState to)
	{
		if (State != from)
		{
			return; // Illegal for the current state: ignore rather than enter an error state.
		}

		MoveTo(to);
	}

	private void MoveTo(RecordingState to)
	{
		RecordingState previous = State;
		State = to;
		StateChanged?.Invoke(this, new RecordingStateChangedEventArgs(previous, to));
	}
}
