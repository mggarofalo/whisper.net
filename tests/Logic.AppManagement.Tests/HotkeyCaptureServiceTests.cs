// Unit depth for the WHISPER-30 capture/rebinding helper, beyond the @WHISPER-30 acceptance scenarios.
// Pins down the one-shot capture resolving a chord (including an extended F13 key), the atomic rebind
// that makes the new chord fire while the old one goes silent, rejection of a bare modifier with the
// previous binding kept, Esc cancellation, and that edges outside listen mode are ignored.

using AwesomeAssertions;
using Domain.Input;
using Domain.Settings;
using Logic.AppManagement;
using Xunit;

namespace Logic.AppManagement.Tests;

public sealed class HotkeyCaptureServiceTests
{
	private readonly HotkeyActivationController _controller = new();
	private readonly HotkeyCaptureService _capture;
	private HotkeyBinding? _captured;
	private bool _rejected;
	private bool _cancelled;
	private int _starts;

	public HotkeyCaptureServiceTests()
	{
		_capture = new HotkeyCaptureService(_controller);
		_capture.CaptureCompleted += (_, b) => _captured = b;
		_capture.CaptureRejected += (_, _) => _rejected = true;
		_capture.CaptureCancelled += (_, _) => _cancelled = true;
		_controller.RecordingStartRequested += (_, _) => _starts++;
	}

	[Fact]
	public void Captures_a_chord_and_rebinds_atomically()
	{
		_capture.BeginCapture();
		CaptureDown(KeyboardKey.Control, KeyModifiers.Control);
		CaptureDown(KeyboardKey.Alt, KeyModifiers.Control | KeyModifiers.Alt);
		CaptureDown(KeyboardKey.R, KeyModifiers.Control | KeyModifiers.Alt);

		_captured.Should().Be(HotkeyBinding.Parse("Ctrl+Alt+R"));
		_controller.Binding.Should().Be(HotkeyBinding.Parse("Ctrl+Alt+R"));
		_capture.IsCapturing.Should().BeFalse();

		// The old default chord (Ctrl+Win) no longer triggers...
		HoldOnController(("Control", KeyModifiers.Control), ("Win", KeyModifiers.Control | KeyModifiers.Win));
		_starts.Should().Be(0);

		// ...while the newly bound chord does.
		HoldOnController(
			("Control", KeyModifiers.Control),
			("Alt", KeyModifiers.Control | KeyModifiers.Alt),
			("R", KeyModifiers.Control | KeyModifiers.Alt));
		_starts.Should().Be(1);
	}

	[Fact]
	public void Captures_an_extended_key()
	{
		_capture.BeginCapture();
		CaptureDown(KeyboardKey.F13, KeyModifiers.None);

		_captured.Should().Be(HotkeyBinding.Parse("F13"));
		_controller.Binding.PrimaryKey.Should().Be(KeyboardKey.F13);
	}

	[Fact]
	public void A_bare_modifier_is_rejected_and_the_previous_binding_is_kept()
	{
		_capture.BeginCapture();
		CaptureDown(KeyboardKey.Control, KeyModifiers.Control);
		CaptureUp(KeyboardKey.Control, KeyModifiers.None);

		_rejected.Should().BeTrue();
		_captured.Should().BeNull();
		_controller.Binding.Should().Be(HotkeyBinding.Parse("Ctrl+Win")); // unchanged default
	}

	[Fact]
	public void Esc_cancels_capture_and_keeps_the_binding()
	{
		_capture.BeginCapture();
		CaptureDown(KeyboardKey.Escape, KeyModifiers.None);

		_cancelled.Should().BeTrue();
		_captured.Should().BeNull();
		_capture.IsCapturing.Should().BeFalse();
		_controller.Binding.Should().Be(HotkeyBinding.Parse("Ctrl+Win"));
	}

	[Fact]
	public void Key_edges_outside_listen_mode_are_ignored()
	{
		// No BeginCapture: nothing should resolve.
		CaptureDown(KeyboardKey.F13, KeyModifiers.None);

		_captured.Should().BeNull();
		_rejected.Should().BeFalse();
		_cancelled.Should().BeFalse();
	}

	private void CaptureDown(KeyboardKey key, KeyModifiers modifiers) => _capture.HandleKeyDown(key, modifiers);

	private void CaptureUp(KeyboardKey key, KeyModifiers modifiers) => _capture.HandleKeyUp(key, modifiers);

	private void HoldOnController(params (string Key, KeyModifiers Modifiers)[] edges)
	{
		_controller.Configure(_controller.Binding, _controller.Mode); // reset live chord state before holding
		foreach ((string keyName, KeyModifiers modifiers) in edges)
		{
			_controller.HandleKeyDown(ParseKey(keyName), modifiers);
		}
	}

	private static KeyboardKey ParseKey(string name) => name.ToLowerInvariant() switch
	{
		"control" => KeyboardKey.Control,
		"win" => KeyboardKey.Win,
		_ => Enum.Parse<KeyboardKey>(name, ignoreCase: true),
	};
}
