// The Driver owns HOW clipboard-paste delivery is exercised, so the steps stay one-liners.
// It drives the REAL ClipboardTextInjector over fake clipboard and keyboard seams, captures what was on
// the clipboard at the moment Ctrl+V fired (that is what "pasted into the focused window" means at this
// boundary), and can simulate another process copying during the paste to prove the restore guard.

using Application.Ports;
using AwesomeAssertions;
using Dictation.Specs.Support;
using Infrastructure.TextDelivery;

namespace Dictation.Specs.Drivers;

public sealed class ClipboardDeliveryDriver
{
	private readonly FakeClipboard _clipboard = new();
	private readonly FakeKeyboardInput _keyboard = new();
	private readonly ITextInjector _injector;
	private string? _pastedContent;
	private string _pendingText = string.Empty;
	private bool _delivered;

	public ClipboardDeliveryDriver()
	{
		_injector = new ClipboardTextInjector(_clipboard, _keyboard);
		// Capture what a focused window would receive on paste: the clipboard contents when Ctrl+V fires.
		_keyboard.OnSend = SnapshotPastedContent;
	}

	public void ClipboardContains(string content) => _clipboard.Seed(content);

	public void AnotherProcessCopiesDuringDelivery(string content) =>
		_keyboard.OnSend = () =>
		{
			// The paste reads our text first; then the other process copies in, before the restore runs.
			SnapshotPastedContent();
			_clipboard.ExternalCopy(content);
		};

	// Delivery is deferred so a "during delivery" step can arrange its hook regardless of step order;
	// the first assertion runs the real injection exactly once.
	public void Deliver(string text) => _pendingText = text;

	public void AssertPasted(string expected)
	{
		EnsureDelivered();
		_pastedContent.Should().Be(expected);
		_keyboard.SentCtrlV.Should().BeTrue();
	}

	public void AssertClipboardContains(string expected)
	{
		EnsureDelivered();
		_clipboard.GetText().Should().Be(expected);
	}

	private void SnapshotPastedContent() => _pastedContent ??= _clipboard.GetText();

	private void EnsureDelivered()
	{
		if (_delivered)
		{
			return;
		}

		_delivered = true;
		_injector.Inject(_pendingText);
	}
}
