// The ITextInjector adapter that types text into the focused window by synthesizing Unicode
// keystrokes (WHISPER-2). This is the universal delivery path: unlike clipboard paste, which many
// terminals ignore, KEYEVENTF_UNICODE typing lands in terminals, browsers, IDEs and chat boxes alike.
//
// The adapter owns only the device-independent decomposition of a string into key events; the actual
// SendInput call lives behind IKeyboardInput (Win32KeyboardInput in production, a fake in tests). Each
// UTF-16 code unit becomes a down/up pair injected as a character, so surrogate pairs (emoji, non-BMP)
// flow through as their two code units without special handling. Line breaks are the one mapping: CRLF
// and lone CR are normalized to LF, and every LF is sent as a real Enter keypress (VK_RETURN) rather
// than a literal U+000A, which is what callers actually want at a line break. Every other character —
// tabs included — is typed literally as its code unit.

using Application.Ports;

namespace Infrastructure.TextDelivery;

public sealed class SendInputTextInjector(IKeyboardInput keyboard) : ITextInjector
{
	// Win32 virtual-key code for Enter; sent as a real keypress so line breaks behave as Enter.
	private const ushort VkReturn = 0x0D;

	public void Inject(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return;
		}

		// Two events (down + up) per code unit is the worst case; most strings hit exactly that.
		List<KeyEvent> events = new(text.Length * 2);
		foreach (char unit in NormalizeNewlines(text))
		{
			if (unit == '\n')
			{
				events.Add(new KeyEvent(VkReturn, IsUnicode: false, KeyAction.Down));
				events.Add(new KeyEvent(VkReturn, IsUnicode: false, KeyAction.Up));
			}
			else
			{
				events.Add(new KeyEvent(unit, IsUnicode: true, KeyAction.Down));
				events.Add(new KeyEvent(unit, IsUnicode: true, KeyAction.Up));
			}
		}

		keyboard.Send(events);
	}

	// Collapse every line-break form to a single LF so each maps to exactly one Enter keypress.
	private static string NormalizeNewlines(string text) =>
		text.Replace("\r\n", "\n").Replace('\r', '\n');
}
