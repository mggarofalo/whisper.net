// Infrastructure-internal seam over the OS keystroke-synthesis call (Win32 SendInput). Splitting it
// out lets the injector's text -> key-event logic — Unicode/surrogate expansion and newline mapping —
// run and be tested without synthesizing real input; Win32KeyboardInput wraps the real API, while the
// specs and unit tests feed a recording fake. Like IWhisperEngine / IAudioCaptureClient, the seam is
// public so adapters in the same layer compose over it and tests can substitute it.

namespace Infrastructure.TextDelivery;

/// <summary>Whether a synthetic key event presses a key down or releases it.</summary>
public enum KeyAction
{
	/// <summary>The key is pressed.</summary>
	Down,

	/// <summary>The key is released.</summary>
	Up,
}

/// <summary>
/// One synthetic keyboard event. When <see cref="IsUnicode"/> is <c>true</c>, <see cref="Code"/> is a
/// UTF-16 code unit injected as a character (Win32 KEYEVENTF_UNICODE); otherwise <see cref="Code"/> is
/// a Win32 virtual-key code (e.g. VK_RETURN for Enter). Modelling individual down/up events — rather
/// than whole key presses — lets callers express chords such as Ctrl+V, not only standalone keystrokes.
/// </summary>
public readonly record struct KeyEvent(ushort Code, bool IsUnicode, KeyAction Action);

/// <summary>
/// Synthesizes keyboard input into whatever window currently has focus. The real implementation calls
/// Win32 SendInput; the seam exists so the character-to-key-event decomposition above it is testable.
/// </summary>
public interface IKeyboardInput
{
	/// <summary>Synthesizes the given key events, in order, as a single batch.</summary>
	void Send(IReadOnlyList<KeyEvent> events);
}
