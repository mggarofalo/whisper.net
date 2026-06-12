// Turns the raw key-down/key-up stream (from IHotkeyListener, wired in M7) into recording
// start/stop requests for the bound chord. One chord-matching pipeline serves both activation modes —
// push-to-talk (record while held) and toggle (press to start, press to stop) — so the two never grow
// divergent key-matching logic; the mode only chooses which edges request what. Matching is lenient
// about unrelated keys and strict about the binding: every configured modifier must be held and, for
// a chord with a primary key, that key must be down. The toggle latch here is self-contained
// (alternating start/stop); reconciling it with externally-driven state (e.g. an Esc cancel) is the
// orchestration layer's job in M7. One reconcile it DOES own: if the binding is reconfigured while a
// recording is live, it asks that recording to cancel before swapping (WHISPER-126) so the orchestrator
// can never be left stuck Recording with no chord able to stop it.

using Domain.Input;
using Domain.Settings;

namespace Logic.AppManagement;

public sealed class HotkeyActivationController
{
	private HotkeyBinding _binding = AppSettings.Default.Hotkey;
	private ActivationMode _mode = ActivationMode.PushToTalk;

	private bool _primaryDown;
	private bool _chordSatisfied;
	private bool _toggleEngaged;

	// Whether the controller currently believes a recording is in flight — set on a start request, cleared
	// on a stop. It mirrors what the orchestrator thinks, so a reconfigure can tell whether it is about to
	// orphan a live recording (WHISPER-126).
	private bool _recordingActive;

	/// <summary>Raised when the bound chord asks recording to start.</summary>
	public event EventHandler? RecordingStartRequested;

	/// <summary>Raised when the bound chord asks recording to stop.</summary>
	public event EventHandler? RecordingStopRequested;

	/// <summary>Raised when a live recording must be abandoned (the binding changed under it), so the
	/// orchestrator discards the in-flight capture and returns to Idle instead of being left stuck
	/// Recording (WHISPER-126). Distinct from a normal stop: the captured audio is thrown away, not typed.</summary>
	public event EventHandler? RecordingCancelRequested;

	public HotkeyBinding Binding => _binding;

	public ActivationMode Mode => _mode;

	// Point the controller at a binding + mode, resetting all live state so a half-pressed chord or a
	// toggle latch never carries across a reconfigure.
	public void Configure(HotkeyBinding binding, ActivationMode mode)
	{
		// If a chord is currently driving a recording (push-to-talk held, or a toggle engaged), simply
		// resetting the live state below would drop that recording WITHOUT ever asking it to stop —
		// orphaning it in the orchestrator (stuck Recording, so the next start is a no-op and no overlay
		// ever appears, and the abandoned capture later transcribes as a hallucination). Ask the in-flight
		// recording to cancel first, so the pipeline returns to a clean Idle before the binding swaps
		// (WHISPER-126). This commonly happens when the user types the new chord into the capture UI while
		// the old hotkey is still armed on the global hook, then assigns it while the keys are still held.
		if (_recordingActive)
		{
			_recordingActive = false;
			RecordingCancelRequested?.Invoke(this, EventArgs.Empty);
		}

		_binding = binding;
		_mode = mode;
		_primaryDown = false;
		_chordSatisfied = false;
		_toggleEngaged = false;
	}

	public void HandleKeyDown(KeyboardKey key, KeyModifiers modifiers)
	{
		if (key == _binding.PrimaryKey)
		{
			_primaryDown = true;
		}

		Evaluate(modifiers);
	}

	public void HandleKeyUp(KeyboardKey key, KeyModifiers modifiers)
	{
		if (key == _binding.PrimaryKey)
		{
			_primaryDown = false;
		}

		Evaluate(modifiers);
	}

	// Recompute whether the chord is satisfied and act only on a change (an edge). The mode decides
	// what each edge means; the matching above it is identical for both.
	private void Evaluate(KeyModifiers modifiers)
	{
		bool modifiersHeld = (modifiers & _binding.Modifiers) == _binding.Modifiers;
		bool primaryHeld = _binding.PrimaryKey == KeyboardKey.None || _primaryDown;
		bool satisfied = modifiersHeld && primaryHeld;

		if (satisfied == _chordSatisfied)
		{
			return;
		}

		_chordSatisfied = satisfied;

		if (_mode == ActivationMode.PushToTalk)
		{
			Raise(satisfied);
			return;
		}

		// Toggle acts only on the rising edge (a full press); the release edge is ignored.
		if (!satisfied)
		{
			return;
		}

		_toggleEngaged = !_toggleEngaged;
		Raise(_toggleEngaged);
	}

	private void Raise(bool start)
	{
		// Track in-flight state so a reconfigure can tell it would orphan a live recording (WHISPER-126).
		_recordingActive = start;
		(start ? RecordingStartRequested : RecordingStopRequested)?.Invoke(this, EventArgs.Empty);
	}
}
