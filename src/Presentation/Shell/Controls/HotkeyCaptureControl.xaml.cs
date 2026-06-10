// The reusable hotkey-capture control (WHISPER-79): a read-only TextBox that records the next key
// combination the user presses and exposes it as a two-way bindable Chord (the canonical chord string the
// validated HotkeyViewModel.HotkeyInput binds to). All the capture RULES live WPF-free in
// HotkeyCaptureInterpreter; this control is the thin glue that (1) translates the WPF Key + ModifierKeys to
// the Domain vocabulary — resolving the Alt Key.System -> SystemKey case — and (2) suppresses the TextBox's
// own keyboard handling so a shortcut or context menu never eats the keystroke. Verified by smoke, not specs.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Domain;
using Domain.Input;
using Domain.Settings;
using Logic.AppManagement.Shell;

namespace Presentation.Shell.Controls;

public partial class HotkeyCaptureControl : UserControl
{
	public static readonly DependencyProperty ChordProperty = DependencyProperty.Register(
		nameof(Chord),
		typeof(string),
		typeof(HotkeyCaptureControl),
		new FrameworkPropertyMetadata(default(string), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnChordChanged));

	public HotkeyCaptureControl() => InitializeComponent();

	/// <summary>The captured combination as a canonical chord string (e.g. "Ctrl+Alt+K"), two-way bindable.</summary>
	public string? Chord
	{
		get => (string?)GetValue(ChordProperty);
		set => SetValue(ChordProperty, value);
	}

	// Reflect an externally-set chord (e.g. the loaded binding) into the display, spaced for readability.
	private static void OnChordChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
	{
		HotkeyCaptureControl control = (HotkeyCaptureControl)sender;
		control.DisplayBox.Text = ToDisplay((string?)args.NewValue);
	}

	// Selecting the field starts capture; nothing is typed, the next keystroke is recorded instead.
	private void OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => DisplayBox.SelectAll();

	private void OnPreviewKeyDown(object sender, KeyEventArgs e)
	{
		// Always handle the event so the read-only TextBox never acts on the keystroke (no caret moves, no
		// Ctrl+C, no Alt menu activation) — we are capturing, not editing.
		e.Handled = true;

		// Alt-combinations arrive as Key.System with the real key in SystemKey; unwrap that first.
		Key key = e.Key == Key.System ? e.SystemKey : e.Key;

		switch (HotkeyCaptureInterpreter.Interpret(ToModifiers(Keyboard.Modifiers), ToKeyboardKey(key), out HotkeyBinding? binding))
		{
			case HotkeyCaptureInterpreter.CaptureAction.Commit:
				Chord = binding!.Chord;
				DisplayBox.Text = binding.DisplayChord;
				break;

			case HotkeyCaptureInterpreter.CaptureAction.Clear:
				Chord = string.Empty;
				DisplayBox.Text = string.Empty;
				break;

			case HotkeyCaptureInterpreter.CaptureAction.Ignore:
			default:
				break;
		}
	}

	// Render a canonical chord with spaced separators; an unparseable value shows as-is.
	private static string ToDisplay(string? chord)
	{
		if (string.IsNullOrWhiteSpace(chord))
		{
			return string.Empty;
		}

		try
		{
			return HotkeyBinding.Parse(chord).DisplayChord;
		}
		catch (DomainException)
		{
			return chord;
		}
	}

	private static KeyModifiers ToModifiers(ModifierKeys modifiers)
	{
		KeyModifiers result = KeyModifiers.None;

		if (modifiers.HasFlag(ModifierKeys.Control))
		{
			result |= KeyModifiers.Control;
		}

		if (modifiers.HasFlag(ModifierKeys.Shift))
		{
			result |= KeyModifiers.Shift;
		}

		if (modifiers.HasFlag(ModifierKeys.Alt))
		{
			result |= KeyModifiers.Alt;
		}

		if (modifiers.HasFlag(ModifierKeys.Windows))
		{
			result |= KeyModifiers.Win;
		}

		return result;
	}

	// Map the WPF key to the Domain key the binding model speaks. Standalone modifier keys map to their
	// modifier marker so the interpreter ignores them; an unmapped key becomes Unknown, which the validated
	// HotkeyInput then rejects (an unregisterable combination is flagged, not silently applied).
	private static KeyboardKey ToKeyboardKey(Key key) => key switch
	{
		>= Key.A and <= Key.Z => KeyboardKey.A + (key - Key.A),
		>= Key.D0 and <= Key.D9 => KeyboardKey.D0 + (key - Key.D0),
		>= Key.F1 and <= Key.F24 => KeyboardKey.F1 + (key - Key.F1),
		Key.Space => KeyboardKey.Space,
		Key.Enter => KeyboardKey.Enter,
		Key.Tab => KeyboardKey.Tab,
		Key.Escape => KeyboardKey.Escape,
		Key.Back => KeyboardKey.Backspace,
		Key.Delete => KeyboardKey.Delete,
		Key.LeftCtrl or Key.RightCtrl => KeyboardKey.Control,
		Key.LeftShift or Key.RightShift => KeyboardKey.Shift,
		Key.LeftAlt or Key.RightAlt => KeyboardKey.Alt,
		Key.LWin or Key.RWin => KeyboardKey.Win,
		Key.None => KeyboardKey.None,
		_ => KeyboardKey.Unknown,
	};
}
