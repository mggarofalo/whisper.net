// Helpers over KeyboardKey shared by the hotkey listener (which tracks the live modifier set) and the
// binding model (which parses a chord into its modifiers). Centralizing the key→modifier mapping here
// keeps the two from drifting.

namespace Domain.Input;

public static class KeyboardKeyExtensions
{
	/// <summary>
	/// The modifier flag a key contributes when held, or <see cref="KeyModifiers.None"/> for a
	/// non-modifier key.
	/// </summary>
	public static KeyModifiers AsModifier(this KeyboardKey key) => key switch
	{
		KeyboardKey.Control => KeyModifiers.Control,
		KeyboardKey.Shift => KeyModifiers.Shift,
		KeyboardKey.Alt => KeyModifiers.Alt,
		KeyboardKey.Win => KeyModifiers.Win,
		_ => KeyModifiers.None,
	};
}
