// Pins down the WHISPER-5 clipboard-paste policy over fake seams (no real clipboard or input): the
// delivered text is on the clipboard at the moment Ctrl+V fires, the user's prior contents are restored
// afterward, and — crucially — a concurrent copy that lands during delivery is never clobbered. The
// guard is the change count: restore only happens when it is exactly where our own write left it.

using AwesomeAssertions;
using Infrastructure.TextDelivery;
using Xunit;

namespace Infrastructure.Tests.TextDelivery;

public sealed class ClipboardTextInjectorTests
{
	// In-memory clipboard whose change count advances on every write, exactly like the real one.
	private sealed class FakeClipboard : Application.Ports.IClipboard
	{
		private uint _changeCount;

		public string? Text { get; private set; }

		public string? GetText() => Text;

		public void SetText(string text)
		{
			Text = text;
			_changeCount++;
		}

		public uint GetChangeCount() => _changeCount;

		// A third process copying something — same effect on the change count as any other write.
		public void ExternalCopy(string text) => SetText(text);

		// Seed prior contents without it counting against the assertions' narrative.
		public void Seed(string? text)
		{
			Text = text;
			if (text is not null)
			{
				_changeCount++;
			}
		}
	}

	// Records key events and runs an optional hook when a batch is sent, so a test can simulate a
	// concurrent copy happening "during the paste" (between our write and the restore).
	private sealed class RecordingKeyboard : IKeyboardInput
	{
		private readonly Action? _onSend;

		public RecordingKeyboard(Action? onSend = null) => _onSend = onSend;

		public List<KeyEvent> Events { get; } = [];

		public void Send(IReadOnlyList<KeyEvent> events)
		{
			Events.AddRange(events);
			_onSend?.Invoke();
		}

		public bool SentCtrlV =>
			Events.Any(e => e is { Code: 0x11, IsUnicode: false }) &&
			Events.Any(e => e is { Code: 0x56, IsUnicode: false });
	}

	[Fact]
	public void Restores_the_prior_clipboard_after_pasting()
	{
		FakeClipboard clipboard = new();
		clipboard.Seed("important note");
		ClipboardTextInjector injector = new(clipboard, new RecordingKeyboard());

		injector.Inject("dictated text");

		clipboard.Text.Should().Be("important note");
	}

	[Fact]
	public void The_delivered_text_is_on_the_clipboard_when_paste_fires()
	{
		FakeClipboard clipboard = new();
		clipboard.Seed("important note");
		string? atPaste = null;
		RecordingKeyboard keyboard = new(onSend: () => atPaste = clipboard.GetText());
		ClipboardTextInjector injector = new(clipboard, keyboard);

		injector.Inject("dictated text");

		atPaste.Should().Be("dictated text");
		keyboard.SentCtrlV.Should().BeTrue();
	}

	[Fact]
	public void Does_not_clobber_content_copied_during_delivery()
	{
		FakeClipboard clipboard = new();
		clipboard.Seed("old");
		// Simulate another process copying "new" at paste time, before the restore would run.
		RecordingKeyboard keyboard = new(onSend: () => clipboard.ExternalCopy("new"));
		ClipboardTextInjector injector = new(clipboard, keyboard);

		injector.Inject("dictated text");

		clipboard.Text.Should().Be("new");
	}

	[Fact]
	public void Empty_or_non_text_prior_contents_are_handled_without_restoring()
	{
		FakeClipboard clipboard = new();
		clipboard.Seed(null); // nothing (or non-text) on the clipboard beforehand
		ClipboardTextInjector injector = new(clipboard, new RecordingKeyboard());

		Action inject = () => injector.Inject("dictated text");

		inject.Should().NotThrow();
		// Nothing to restore, so our delivered text is simply left in place.
		clipboard.Text.Should().Be("dictated text");
	}
}
