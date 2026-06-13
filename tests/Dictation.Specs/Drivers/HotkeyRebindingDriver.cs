// Drives the hotkey-rebinding scenarios. It owns HOW capture is exercised so the steps
// stay one-liners: it replays key edges into the REAL HotkeyCaptureService (building the live modifier
// set like the listener), records the capture outcome, and — to prove the rebind took effect — drives
// chords into the REAL HotkeyActivationController the capture service rebinds, watching for a recording
// start request. The two real components are the same instances wired by AddAppManagement.

using AwesomeAssertions;
using Domain.Input;
using Domain.Settings;
using Logic.AppManagement;

namespace Dictation.Specs.Drivers;

public sealed class HotkeyRebindingDriver
{
	private readonly HotkeyCaptureService _capture;
	private readonly HotkeyActivationController _controller;
	private KeyModifiers _held;
	private HotkeyBinding? _captured;
	private bool _rejected;
	private bool _cancelled;
	private int _starts;

	public HotkeyRebindingDriver(HotkeyCaptureService capture, HotkeyActivationController controller)
	{
		_capture = capture;
		_controller = controller;
		_capture.CaptureCompleted += (_, binding) => _captured = binding;
		_capture.CaptureRejected += (_, _) => _rejected = true;
		_capture.CaptureCancelled += (_, _) => _cancelled = true;
		_controller.RecordingStartRequested += (_, _) => _starts++;
	}

	public void BeginCapture()
	{
		_capture.BeginCapture();
		_held = KeyModifiers.None;
	}

	// Press a full chord into the capture service: down in order, then up in reverse. Completion fires
	// on the primary-key down; a bare modifier is rejected on the final release.
	public void CaptureChord(string chord)
	{
		string[] tokens = Tokens(chord);

		foreach (string token in tokens)
		{
			KeyboardKey key = ParseKey(token);
			_held |= key.AsModifier();
			_capture.HandleKeyDown(key, _held);
		}

		foreach (string token in tokens.Reverse())
		{
			KeyboardKey key = ParseKey(token);
			_held &= ~key.AsModifier();
			_capture.HandleKeyUp(key, _held);
		}
	}

	// Press and release a single key into the capture service (used for Esc).
	public void CaptureSingleKey(string keyName)
	{
		KeyboardKey key = ParseKey(keyName);
		_capture.HandleKeyDown(key, _held);
		_capture.HandleKeyUp(key, _held);
	}

	// Hold a chord into the activation controller to see whether the (possibly rebound) binding fires.
	public void HoldChordOnController(string chord)
	{
		KeyModifiers held = KeyModifiers.None;
		foreach (string token in Tokens(chord))
		{
			KeyboardKey key = ParseKey(token);
			held |= key.AsModifier();
			_controller.HandleKeyDown(key, held);
		}
	}

	// --- assertions ---

	public void AssertCaptured(string chord) => _captured.Should().Be(HotkeyBinding.Parse(chord));

	public void AssertRejected() => _rejected.Should().BeTrue();

	public void AssertCancelled() => _cancelled.Should().BeTrue();

	public void AssertRecordingTriggered() => _starts.Should().BeGreaterThan(0);

	// --- test-only parsing ---

	private static string[] Tokens(string chord) =>
		chord.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

	private static KeyboardKey ParseKey(string token) => token.ToLowerInvariant() switch
	{
		"ctrl" or "control" => KeyboardKey.Control,
		"shift" => KeyboardKey.Shift,
		"alt" => KeyboardKey.Alt,
		"win" => KeyboardKey.Win,
		"esc" or "escape" => KeyboardKey.Escape,
		_ => Enum.Parse<KeyboardKey>(token, ignoreCase: true),
	};
}
