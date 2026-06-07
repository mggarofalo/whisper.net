// A keyboard key as the domain understands it — a side-agnostic, OS-independent identity for a single
// physical key. Raw OS key codes (SharpHook's VcXxx) are translated into these before crossing into
// Application, so no Infrastructure key type leaks inward. Includes F13–F24 and the standalone
// modifier keys, which are exactly the keys RegisterHotKey could not bind cleanly and the reason the
// app uses a global hook. Keys outside this set translate to <see cref="Unknown"/> rather than being
// dropped, so an unmapped press is observable instead of silently lost.

namespace Domain.Input;

public enum KeyboardKey
{
	/// <summary>No key.</summary>
	None = 0,

	/// <summary>A key with no domain mapping. Observed, but matches no binding.</summary>
	Unknown,

	// Standalone modifier keys. A modifier press is reported as its key plus the corresponding
	// KeyModifiers flag so chords built purely from modifiers (the default "Ctrl+Win") still resolve.
	Control,
	Shift,
	Alt,
	Win,

	// Letters.
	A, B, C, D, E, F, G, H, I, J, K, L, M,
	N, O, P, Q, R, S, T, U, V, W, X, Y, Z,

	// Top-row digits.
	D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,

	// Function keys, including the extended F13–F24 range.
	F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
	F13, F14, F15, F16, F17, F18, F19, F20, F21, F22, F23, F24,

	// Common editing/whitespace keys.
	Space,
	Enter,
	Tab,
	Escape,
	Backspace,
	Delete,
}
