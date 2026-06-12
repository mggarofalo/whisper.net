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
	private int _cancels;

	public HotkeyActivationControllerTests()
	{
		_controller.RecordingStartRequested += (_, _) => _starts++;
		_controller.RecordingStopRequested += (_, _) => _stops++;
		_controller.RecordingCancelRequested += (_, _) => _cancels++;
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

	// WHISPER-126: a reconfigure that lands while a chord is actively driving a recording must not silently
	// drop it (that orphans the recording in the orchestrator). It asks the live recording to cancel —
	// discarding, not stopping-and-transcribing — so the pipeline can return to a clean Idle.
	[Fact]
	public void Reconfiguring_while_push_to_talk_is_recording_requests_a_cancel()
	{
		Configure("Ctrl+Win", ActivationMode.PushToTalk);
		Hold(KeyboardKey.Control, KeyModifiers.Control);
		Hold(KeyboardKey.Win, KeyModifiers.Control | KeyModifiers.Win); // chord satisfied -> recording
		_starts.Should().Be(1);

		Configure("Ctrl+Alt+J", ActivationMode.PushToTalk);

		_cancels.Should().Be(1);
		_stops.Should().Be(0, "a reconfigured-out recording is discarded, not transcribed");
	}

	[Fact]
	public void Reconfiguring_while_a_toggle_is_engaged_requests_a_cancel()
	{
		Configure("F13", ActivationMode.Toggle);
		FullPressF13(); // engages the toggle -> recording
		_starts.Should().Be(1);

		Configure("F14", ActivationMode.Toggle);

		_cancels.Should().Be(1);
		_stops.Should().Be(0);
	}

	[Fact]
	public void Reconfiguring_while_idle_does_not_request_a_cancel()
	{
		Configure("Ctrl+Win", ActivationMode.PushToTalk);

		Configure("Ctrl+Alt+J", ActivationMode.PushToTalk);

		_cancels.Should().Be(0);
	}

	[Fact]
	public void After_a_reconfigure_under_a_live_recording_the_new_chord_starts_a_fresh_recording()
	{
		Configure("Ctrl+Win", ActivationMode.PushToTalk);
		Hold(KeyboardKey.Control, KeyModifiers.Control);
		Hold(KeyboardKey.Win, KeyModifiers.Control | KeyModifiers.Win);
		Configure("Ctrl+Alt+J", ActivationMode.PushToTalk); // cancels the orphan
		_cancels.Should().Be(1);

		// The new chord drives a clean new recording (the controller is not left half-latched).
		Hold(KeyboardKey.Control, KeyModifiers.Control);
		Hold(KeyboardKey.Alt, KeyModifiers.Control | KeyModifiers.Alt);
		Hold(KeyboardKey.J, KeyModifiers.Control | KeyModifiers.Alt);

		_starts.Should().Be(2);
		_cancels.Should().Be(1);
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
