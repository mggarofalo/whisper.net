// Drives the @WHISPER-16 activation-mode scenarios. It owns HOW a chord is exercised so the steps stay
// one-liners: it configures the REAL HotkeyActivationController with a binding + mode, then replays the
// key-down/key-up edges a chord produces — building up the live modifier set exactly as the listener
// would — and records the recording start/stop requests the controller raises. Asserting at that
// request boundary is the behavior under test; the actual recording state machine is M5-3.

using AwesomeAssertions;
using Domain.Input;
using Domain.Settings;
using Logic.AppManagement;

namespace Dictation.Specs.Drivers;

public sealed class HotkeyActivationDriver
{
	private readonly HotkeyActivationController _controller;
	private KeyModifiers _held;
	private int _starts;
	private int _stops;
	private int _cancels;

	public HotkeyActivationDriver(HotkeyActivationController controller)
	{
		_controller = controller;
		_controller.RecordingStartRequested += (_, _) => _starts++;
		_controller.RecordingStopRequested += (_, _) => _stops++;
		_controller.RecordingCancelRequested += (_, _) => _cancels++;
	}

	public void Configure(string binding, ActivationMode mode)
	{
		_controller.Configure(HotkeyBinding.Parse(binding), mode);
		_held = KeyModifiers.None;
	}

	// Reassign the binding without resetting the live modifier set we are tracking — modelling the user
	// assigning a new hotkey while the old chord is still physically held (WHISPER-126).
	public void Reassign(string binding, ActivationMode mode) =>
		_controller.Configure(HotkeyBinding.Parse(binding), mode);

	// Press each token of the chord in order, accumulating modifiers into the live set just like the
	// real listener so the controller sees consistent (key, modifiers) snapshots.
	public void HoldChord(string chord)
	{
		foreach (string token in Tokens(chord))
		{
			KeyboardKey key = ParseKey(token);
			_held |= key.AsModifier();
			_controller.HandleKeyDown(key, _held);
		}
	}

	// Release each token in reverse order, clearing modifiers as they go up.
	public void ReleaseChord(string chord)
	{
		foreach (string token in Tokens(chord).Reverse())
		{
			KeyboardKey key = ParseKey(token);
			_held &= ~key.AsModifier();
			_controller.HandleKeyUp(key, _held);
		}
	}

	public void FullPress(string chord)
	{
		HoldChord(chord);
		ReleaseChord(chord);
	}

	public void PressUnrelatedKey(string key)
	{
		KeyboardKey parsed = ParseKey(key);
		_controller.HandleKeyDown(parsed, _held);
		_controller.HandleKeyUp(parsed, _held);
	}

	// --- assertions (request boundary) ---

	public void AssertStartRequested(int times) => _starts.Should().Be(times);

	public void AssertStopRequested(int times) => _stops.Should().Be(times);

	public void AssertCancelRequested(int times) => _cancels.Should().Be(times);

	// --- test-only parsing ---

	private static string[] Tokens(string chord) =>
		chord.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

	private static KeyboardKey ParseKey(string token) => token.ToLowerInvariant() switch
	{
		"ctrl" or "control" => KeyboardKey.Control,
		"shift" => KeyboardKey.Shift,
		"alt" => KeyboardKey.Alt,
		"win" => KeyboardKey.Win,
		_ => Enum.Parse<KeyboardKey>(token, ignoreCase: true),
	};
}
