// Unit depth for the WHISPER-16 activation controller, beyond the @WHISPER-16 acceptance scenarios.
// Pins down the chord-matching edges both modes share: push-to-talk start-on-hold / stop-on-release
// (including a pure-modifier chord), the toggle alternation, and the matching rules — all configured
// modifiers required, unrelated and extra keys ignored, partial chords inert. The controller is fed
// the same (key, live-modifier) snapshots the listener would emit.

using AwesomeAssertions;
using Domain.Input;
using Domain.Settings;
using Logic.AppManagement;
using Xunit;

namespace Logic.AppManagement.Tests;

public sealed class HotkeyActivationControllerTests
{
	private readonly HotkeyActivationController _controller = new();
	private int _starts;
	private int _stops;

	public HotkeyActivationControllerTests()
	{
		_controller.RecordingStartRequested += (_, _) => _starts++;
		_controller.RecordingStopRequested += (_, _) => _stops++;
	}

	[Fact]
	public void Push_to_talk_starts_on_hold_and_stops_on_release()
	{
		Configure("Ctrl+Alt+Space", ActivationMode.PushToTalk);

		Hold(KeyboardKey.Control, KeyModifiers.Control);
		Hold(KeyboardKey.Alt, KeyModifiers.Control | KeyModifiers.Alt);
		Hold(KeyboardKey.Space, KeyModifiers.Control | KeyModifiers.Alt);

		_starts.Should().Be(1);
		_stops.Should().Be(0);

		Release(KeyboardKey.Space, KeyModifiers.Control | KeyModifiers.Alt);

		_stops.Should().Be(1);
	}

	[Fact]
	public void Push_to_talk_works_for_a_pure_modifier_chord()
	{
		Configure("Ctrl+Win", ActivationMode.PushToTalk);

		Hold(KeyboardKey.Control, KeyModifiers.Control);
		_starts.Should().Be(0); // only one of the two modifiers down yet

		Hold(KeyboardKey.Win, KeyModifiers.Control | KeyModifiers.Win);
		_starts.Should().Be(1);

		Release(KeyboardKey.Win, KeyModifiers.Control);
		_stops.Should().Be(1);
	}

	[Fact]
	public void Toggle_alternates_start_and_stop_on_successive_full_presses()
	{
		Configure("F13", ActivationMode.Toggle);

		FullPressF13();
		_starts.Should().Be(1);
		_stops.Should().Be(0);

		FullPressF13();
		_starts.Should().Be(1);
		_stops.Should().Be(1);
	}

	[Fact]
	public void A_partial_chord_does_not_trigger()
	{
		Configure("Ctrl+Alt+Space", ActivationMode.PushToTalk);

		Hold(KeyboardKey.Control, KeyModifiers.Control);
		Hold(KeyboardKey.Alt, KeyModifiers.Control | KeyModifiers.Alt);

		_starts.Should().Be(0);
	}

	[Fact]
	public void Holding_the_modifiers_but_a_different_key_does_not_trigger()
	{
		Configure("Ctrl+Alt+Space", ActivationMode.PushToTalk);

		Hold(KeyboardKey.Control, KeyModifiers.Control);
		Hold(KeyboardKey.Alt, KeyModifiers.Control | KeyModifiers.Alt);
		Hold(KeyboardKey.D, KeyModifiers.Control | KeyModifiers.Alt); // not the primary key

		_starts.Should().Be(0);
	}

	[Fact]
	public void Extra_modifiers_beyond_the_chord_still_satisfy_it()
	{
		Configure("Ctrl+Space", ActivationMode.PushToTalk);

		// Shift is held too, but the chord only requires Ctrl + Space.
		Hold(KeyboardKey.Shift, KeyModifiers.Shift);
		Hold(KeyboardKey.Control, KeyModifiers.Shift | KeyModifiers.Control);
		Hold(KeyboardKey.Space, KeyModifiers.Shift | KeyModifiers.Control);

		_starts.Should().Be(1);
	}

	[Fact]
	public void Reconfiguring_clears_a_half_pressed_chord()
	{
		Configure("Ctrl+Space", ActivationMode.PushToTalk);
		Hold(KeyboardKey.Control, KeyModifiers.Control); // half-pressed

		Configure("F13", ActivationMode.Toggle);

		FullPressF13();
		_starts.Should().Be(1);
		_stops.Should().Be(0);
	}

	private void Configure(string binding, ActivationMode mode) =>
		_controller.Configure(HotkeyBinding.Parse(binding), mode);

	private void Hold(KeyboardKey key, KeyModifiers modifiers) => _controller.HandleKeyDown(key, modifiers);

	private void Release(KeyboardKey key, KeyModifiers modifiers) => _controller.HandleKeyUp(key, modifiers);

	private void FullPressF13()
	{
		_controller.HandleKeyDown(KeyboardKey.F13, KeyModifiers.None);
		_controller.HandleKeyUp(KeyboardKey.F13, KeyModifiers.None);
	}
}
