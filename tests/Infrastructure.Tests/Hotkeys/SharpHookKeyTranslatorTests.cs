// Unit depth for the WHISPER-10 raw-key translation: SharpHook's VcXxx codes map to the right Domain
// keys, left/right modifier variants collapse to one side-agnostic key, the extended F13 range is
// preserved (the reason the app uses a global hook at all), and anything unmapped becomes Unknown
// rather than being silently dropped. Pure and total, so it is exhaustively checkable here.

using AwesomeAssertions;
using Domain.Input;
using Infrastructure.Hotkeys;
using SharpHook.Data;
using Xunit;

namespace Infrastructure.Tests.Hotkeys;

public sealed class SharpHookKeyTranslatorTests
{
	[Theory]
	[InlineData(KeyCode.VcLeftControl, KeyboardKey.Control)]
	[InlineData(KeyCode.VcRightControl, KeyboardKey.Control)]
	[InlineData(KeyCode.VcLeftShift, KeyboardKey.Shift)]
	[InlineData(KeyCode.VcRightShift, KeyboardKey.Shift)]
	[InlineData(KeyCode.VcLeftAlt, KeyboardKey.Alt)]
	[InlineData(KeyCode.VcRightAlt, KeyboardKey.Alt)]
	[InlineData(KeyCode.VcLeftMeta, KeyboardKey.Win)]
	[InlineData(KeyCode.VcRightMeta, KeyboardKey.Win)]
	public void Modifier_keys_collapse_to_one_side_agnostic_key(KeyCode code, KeyboardKey expected) =>
		SharpHookKeyTranslator.Translate(code).Should().Be(expected);

	[Theory]
	[InlineData(KeyCode.VcA, KeyboardKey.A)]
	[InlineData(KeyCode.VcZ, KeyboardKey.Z)]
	[InlineData(KeyCode.Vc0, KeyboardKey.D0)]
	[InlineData(KeyCode.Vc9, KeyboardKey.D9)]
	[InlineData(KeyCode.VcF1, KeyboardKey.F1)]
	[InlineData(KeyCode.VcF12, KeyboardKey.F12)]
	[InlineData(KeyCode.VcF13, KeyboardKey.F13)]
	[InlineData(KeyCode.VcF24, KeyboardKey.F24)]
	[InlineData(KeyCode.VcSpace, KeyboardKey.Space)]
	[InlineData(KeyCode.VcEnter, KeyboardKey.Enter)]
	[InlineData(KeyCode.VcTab, KeyboardKey.Tab)]
	[InlineData(KeyCode.VcEscape, KeyboardKey.Escape)]
	[InlineData(KeyCode.VcBackspace, KeyboardKey.Backspace)]
	[InlineData(KeyCode.VcDelete, KeyboardKey.Delete)]
	public void Letters_digits_function_and_editing_keys_map_to_their_domain_key(KeyCode code, KeyboardKey expected) =>
		SharpHookKeyTranslator.Translate(code).Should().Be(expected);

	[Theory]
	[InlineData(KeyCode.VcUndefined)]
	[InlineData(KeyCode.VcHome)]
	public void Unmapped_keys_become_Unknown_rather_than_being_dropped(KeyCode code) =>
		SharpHookKeyTranslator.Translate(code).Should().Be(KeyboardKey.Unknown);
}
