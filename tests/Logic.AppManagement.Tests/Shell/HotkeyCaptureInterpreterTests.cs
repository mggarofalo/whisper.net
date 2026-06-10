// Inner TDD loop for the hotkey-capture control's WPF-free brain (WHISPER-79). The control is thin glue;
// these pin the capture rules it delegates: a full combination commits as a canonical binding (spaced for
// display), a standalone modifier is ignored so the user can build a chord, Esc/Backspace clears, and an
// unmapped key still commits as an Unknown-key chord that the validated HotkeyInput later rejects.

using AwesomeAssertions;
using Domain.Input;
using Domain.Settings;
using Logic.AppManagement.Shell;
using Xunit;

namespace Logic.AppManagement.Tests.Shell;

public sealed class HotkeyCaptureInterpreterTests
{
	[Fact]
	public void A_full_combination_commits_as_a_spaced_canonical_binding()
	{
		HotkeyCaptureInterpreter.CaptureAction action =
			HotkeyCaptureInterpreter.Interpret(KeyModifiers.Control | KeyModifiers.Alt, KeyboardKey.K, out HotkeyBinding? binding);

		action.Should().Be(HotkeyCaptureInterpreter.CaptureAction.Commit);
		binding!.Chord.Should().Be("Ctrl+Alt+K");
		binding.DisplayChord.Should().Be("Ctrl + Alt + K");
	}

	[Theory]
	[InlineData(KeyboardKey.Control)]
	[InlineData(KeyboardKey.Shift)]
	[InlineData(KeyboardKey.Alt)]
	[InlineData(KeyboardKey.Win)]
	[InlineData(KeyboardKey.None)]
	public void A_standalone_modifier_or_no_key_is_ignored(KeyboardKey key)
	{
		HotkeyCaptureInterpreter.CaptureAction action =
			HotkeyCaptureInterpreter.Interpret(KeyModifiers.Control, key, out HotkeyBinding? binding);

		action.Should().Be(HotkeyCaptureInterpreter.CaptureAction.Ignore);
		binding.Should().BeNull();
	}

	[Theory]
	[InlineData(KeyboardKey.Escape)]
	[InlineData(KeyboardKey.Backspace)]
	public void Escape_or_backspace_clears(KeyboardKey key)
	{
		HotkeyCaptureInterpreter.CaptureAction action =
			HotkeyCaptureInterpreter.Interpret(KeyModifiers.None, key, out HotkeyBinding? binding);

		action.Should().Be(HotkeyCaptureInterpreter.CaptureAction.Clear);
		binding.Should().BeNull();
	}

	[Fact]
	public void An_unmapped_key_commits_as_an_unknown_chord_for_validation_to_reject()
	{
		HotkeyCaptureInterpreter.CaptureAction action =
			HotkeyCaptureInterpreter.Interpret(KeyModifiers.Control, KeyboardKey.Unknown, out HotkeyBinding? binding);

		action.Should().Be(HotkeyCaptureInterpreter.CaptureAction.Commit);
		binding!.PrimaryKey.Should().Be(KeyboardKey.Unknown, "an unregisterable combo is captured but flagged invalid downstream");
	}
}
