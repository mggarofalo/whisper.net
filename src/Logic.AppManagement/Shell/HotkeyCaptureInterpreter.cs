// The WPF-free brain of the reusable hotkey-capture control (WHISPER-79). The control is a thin WPF
// UserControl: on each PreviewKeyDown it maps the WPF Key (resolving the Alt Key.System -> SystemKey case)
// and the live ModifierKeys to the Domain vocabulary, then asks this interpreter what to do. Keeping the
// decision here — not in the control — means the capture rules (ignore a standalone modifier, complete on a
// real key, clear on Esc/Backspace) are unit-tested without WPF, and the control stays glue. A completed
// capture yields a HotkeyBinding whose Chord feeds the validated HotkeyViewModel.HotkeyInput, so an
// unregisterable combination (an unmapped key) is flagged by the existing validation and never applied.

using Domain.Input;
using Domain.Settings;

namespace Logic.AppManagement.Shell;

public static class HotkeyCaptureInterpreter
{
	/// <summary>The outcome of interpreting one key-down while capturing.</summary>
	public enum CaptureAction
	{
		/// <summary>A standalone modifier (or no key): keep waiting for a real key, change nothing.</summary>
		Ignore,

		/// <summary>Esc or Backspace: clear the current capture.</summary>
		Clear,

		/// <summary>A complete chord was captured (see the out binding).</summary>
		Commit,
	}

	// Decide what a key-down means during capture. A standalone modifier (or None) is ignored so the user can
	// build "Ctrl + Alt + …" without it committing early; Esc/Backspace clears; any other key completes the
	// chord with whatever modifiers are currently held. The binding is canonical, so equivalent presses
	// ("Win+Ctrl+K" / "Ctrl+Win+K") capture identically.
	public static CaptureAction Interpret(KeyModifiers modifiers, KeyboardKey key, out HotkeyBinding? binding)
	{
		binding = null;

		if (key is KeyboardKey.Escape or KeyboardKey.Backspace)
		{
			return CaptureAction.Clear;
		}

		if (key == KeyboardKey.None || key.AsModifier() != KeyModifiers.None)
		{
			return CaptureAction.Ignore;
		}

		binding = HotkeyBinding.FromKeys(modifiers, key);
		return CaptureAction.Commit;
	}
}
