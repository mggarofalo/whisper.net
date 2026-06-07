// The modifier keys that can accompany a keystroke, as a bit set so a chord can hold any combination
// (e.g. Control | Alt). Lives in Domain so the hotkey listener (Infrastructure), the activation logic
// (Logic.AppManagement), and the binding model all speak one vocabulary — left/right variants are
// collapsed to a single side-agnostic flag because a binding never cares which Ctrl was pressed.

namespace Domain.Input;

[Flags]
public enum KeyModifiers
{
	/// <summary>No modifier keys are held.</summary>
	None = 0,

	/// <summary>Either Control key is held.</summary>
	Control = 1 << 0,

	/// <summary>Either Shift key is held.</summary>
	Shift = 1 << 1,

	/// <summary>Either Alt key is held.</summary>
	Alt = 1 << 2,

	/// <summary>Either Windows (Super/Meta) key is held.</summary>
	Win = 1 << 3,
}
