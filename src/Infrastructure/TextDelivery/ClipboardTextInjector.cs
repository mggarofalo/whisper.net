// The ITextInjector adapter that delivers text by pasting (the typing path is the default;
// this is the alternative for very long text or when the user picks paste). The clipboard is shared
// global state, so the cardinal rule is: never clobber whatever the user copied. We snapshot the prior
// contents and the clipboard change count, set our text, issue Ctrl+V, then restore the prior contents
// — but only if the change count is exactly where our own write left it. If it advanced (a concurrent
// copy landed between our paste and the restore), the newer content wins and we leave it untouched.
//
// Like SendInputTextInjector this owns only the device-independent policy; clipboard access is behind
// IClipboard and the Ctrl+V keystroke behind IKeyboardInput, so the restore-guard is fully testable.

using Application.Ports;

namespace Infrastructure.TextDelivery;

public sealed class ClipboardTextInjector(IClipboard clipboard, IKeyboardInput keyboard) : ITextInjector
{
	private const ushort VkControl = 0x11;
	private const ushort VkV = 0x56;

	public void Inject(string text)
	{
		// Snapshot what the user had before we touch anything. A null prior means non-text or empty
		// clipboard contents — there is nothing to restore, so we simply leave our delivered text.
		string? prior = clipboard.GetText();

		clipboard.SetText(text);
		uint changeCountAfterOurWrite = clipboard.GetChangeCount();

		Paste();

		// Restore only if no other process changed the clipboard since our write (the change count is
		// unchanged) and there was restorable text. Otherwise the newer content is preserved.
		if (prior is not null && clipboard.GetChangeCount() == changeCountAfterOurWrite)
		{
			clipboard.SetText(prior);
		}
	}

	// Synthesize Ctrl+V: Ctrl down, V down, V up, Ctrl up. V is sent as a virtual key (not a Unicode
	// character) so it forms the paste shortcut rather than typing the letter 'v'.
	private void Paste() => keyboard.Send(
	[
		new KeyEvent(VkControl, IsUnicode: false, KeyAction.Down),
		new KeyEvent(VkV, IsUnicode: false, KeyAction.Down),
		new KeyEvent(VkV, IsUnicode: false, KeyAction.Up),
		new KeyEvent(VkControl, IsUnicode: false, KeyAction.Up),
	]);
}
