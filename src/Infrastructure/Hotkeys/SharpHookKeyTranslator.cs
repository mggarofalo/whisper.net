// Pure translation from SharpHook's raw KeyCode to the Domain KeyboardKey. This is the boundary that
// keeps SharpHook types from leaking inward: above it the app speaks only Domain.Input. Left/right
// modifier variants collapse to a single side-agnostic key because a binding never cares which Ctrl
// was pressed; anything outside the mapped set becomes Unknown so an unhandled key is still observed,
// never silently dropped. Total and deterministic — trivially unit-testable without a hook.

using Domain.Input;
using SharpHook.Data;

namespace Infrastructure.Hotkeys;

public static class SharpHookKeyTranslator
{
	public static KeyboardKey Translate(KeyCode code) => code switch
	{
		KeyCode.VcLeftControl or KeyCode.VcRightControl => KeyboardKey.Control,
		KeyCode.VcLeftShift or KeyCode.VcRightShift => KeyboardKey.Shift,
		KeyCode.VcLeftAlt or KeyCode.VcRightAlt => KeyboardKey.Alt,
		KeyCode.VcLeftMeta or KeyCode.VcRightMeta => KeyboardKey.Win,

		KeyCode.VcA => KeyboardKey.A,
		KeyCode.VcB => KeyboardKey.B,
		KeyCode.VcC => KeyboardKey.C,
		KeyCode.VcD => KeyboardKey.D,
		KeyCode.VcE => KeyboardKey.E,
		KeyCode.VcF => KeyboardKey.F,
		KeyCode.VcG => KeyboardKey.G,
		KeyCode.VcH => KeyboardKey.H,
		KeyCode.VcI => KeyboardKey.I,
		KeyCode.VcJ => KeyboardKey.J,
		KeyCode.VcK => KeyboardKey.K,
		KeyCode.VcL => KeyboardKey.L,
		KeyCode.VcM => KeyboardKey.M,
		KeyCode.VcN => KeyboardKey.N,
		KeyCode.VcO => KeyboardKey.O,
		KeyCode.VcP => KeyboardKey.P,
		KeyCode.VcQ => KeyboardKey.Q,
		KeyCode.VcR => KeyboardKey.R,
		KeyCode.VcS => KeyboardKey.S,
		KeyCode.VcT => KeyboardKey.T,
		KeyCode.VcU => KeyboardKey.U,
		KeyCode.VcV => KeyboardKey.V,
		KeyCode.VcW => KeyboardKey.W,
		KeyCode.VcX => KeyboardKey.X,
		KeyCode.VcY => KeyboardKey.Y,
		KeyCode.VcZ => KeyboardKey.Z,

		KeyCode.Vc0 => KeyboardKey.D0,
		KeyCode.Vc1 => KeyboardKey.D1,
		KeyCode.Vc2 => KeyboardKey.D2,
		KeyCode.Vc3 => KeyboardKey.D3,
		KeyCode.Vc4 => KeyboardKey.D4,
		KeyCode.Vc5 => KeyboardKey.D5,
		KeyCode.Vc6 => KeyboardKey.D6,
		KeyCode.Vc7 => KeyboardKey.D7,
		KeyCode.Vc8 => KeyboardKey.D8,
		KeyCode.Vc9 => KeyboardKey.D9,

		KeyCode.VcF1 => KeyboardKey.F1,
		KeyCode.VcF2 => KeyboardKey.F2,
		KeyCode.VcF3 => KeyboardKey.F3,
		KeyCode.VcF4 => KeyboardKey.F4,
		KeyCode.VcF5 => KeyboardKey.F5,
		KeyCode.VcF6 => KeyboardKey.F6,
		KeyCode.VcF7 => KeyboardKey.F7,
		KeyCode.VcF8 => KeyboardKey.F8,
		KeyCode.VcF9 => KeyboardKey.F9,
		KeyCode.VcF10 => KeyboardKey.F10,
		KeyCode.VcF11 => KeyboardKey.F11,
		KeyCode.VcF12 => KeyboardKey.F12,
		KeyCode.VcF13 => KeyboardKey.F13,
		KeyCode.VcF14 => KeyboardKey.F14,
		KeyCode.VcF15 => KeyboardKey.F15,
		KeyCode.VcF16 => KeyboardKey.F16,
		KeyCode.VcF17 => KeyboardKey.F17,
		KeyCode.VcF18 => KeyboardKey.F18,
		KeyCode.VcF19 => KeyboardKey.F19,
		KeyCode.VcF20 => KeyboardKey.F20,
		KeyCode.VcF21 => KeyboardKey.F21,
		KeyCode.VcF22 => KeyboardKey.F22,
		KeyCode.VcF23 => KeyboardKey.F23,
		KeyCode.VcF24 => KeyboardKey.F24,

		KeyCode.VcSpace => KeyboardKey.Space,
		KeyCode.VcEnter => KeyboardKey.Enter,
		KeyCode.VcTab => KeyboardKey.Tab,
		KeyCode.VcEscape => KeyboardKey.Escape,
		KeyCode.VcBackspace => KeyboardKey.Backspace,
		KeyCode.VcDelete => KeyboardKey.Delete,

		_ => KeyboardKey.Unknown,
	};
}
