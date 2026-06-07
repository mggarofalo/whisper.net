// Edge-case depth for the WHISPER-2 text injector, beyond the @WHISPER-2 acceptance scenarios. Drives
// the real SendInputTextInjector over a recording fake keyboard (no real input) and pins down the
// contract that matters: every character becomes a down/up pair, the down events reconstruct the exact
// input string (including BMP punctuation and non-BMP surrogate pairs), line-break forms collapse to a
// single Enter keypress, tabs stay literal, and empty input synthesizes nothing.

using System.Text;
using AwesomeAssertions;
using Infrastructure.TextDelivery;
using Xunit;

namespace Infrastructure.Tests.TextDelivery;

public sealed class SendInputTextInjectorTests
{
	// Records the events the injector synthesizes and reconstructs the text a focused field would see:
	// each Down event is a character (Unicode code unit) or an Enter (VK_RETURN -> newline); Up events
	// mirror them and are ignored when rebuilding the text.
	private sealed class RecordingKeyboard : IKeyboardInput
	{
		private const ushort VkReturn = 0x0D;

		public List<KeyEvent> Events { get; } = [];
		public int SendCalls { get; private set; }

		public void Send(IReadOnlyList<KeyEvent> events)
		{
			SendCalls++;
			Events.AddRange(events);
		}

		public string ReconstructTypedText()
		{
			StringBuilder builder = new();
			foreach (KeyEvent e in Events.Where(e => e.Action == KeyAction.Down))
			{
				builder.Append(e.IsUnicode ? (char)e.Code : e.Code == VkReturn ? '\n' : '�');
			}

			return builder.ToString();
		}
	}

	[Theory]
	[InlineData("hello")]
	[InlineData("café ✓")]
	[InlineData("ls -la && echo \"done\"")]
	public void Types_the_exact_characters(string text)
	{
		RecordingKeyboard keyboard = new();
		SendInputTextInjector injector = new(keyboard);

		injector.Inject(text);

		keyboard.ReconstructTypedText().Should().Be(text);
	}

	[Fact]
	public void Each_character_is_a_down_then_up_pair()
	{
		RecordingKeyboard keyboard = new();
		SendInputTextInjector injector = new(keyboard);

		injector.Inject("ab");

		keyboard.Events.Should().HaveCount(4);
		keyboard.Events[0].Should().Be(new KeyEvent('a', IsUnicode: true, KeyAction.Down));
		keyboard.Events[1].Should().Be(new KeyEvent('a', IsUnicode: true, KeyAction.Up));
		keyboard.Events[2].Should().Be(new KeyEvent('b', IsUnicode: true, KeyAction.Down));
		keyboard.Events[3].Should().Be(new KeyEvent('b', IsUnicode: true, KeyAction.Up));
	}

	[Fact]
	public void Non_bmp_characters_are_typed_as_their_surrogate_code_units()
	{
		RecordingKeyboard keyboard = new();
		SendInputTextInjector injector = new(keyboard);

		injector.Inject("I 👍 it");

		// The thumbs-up is U+1F44D, which is the surrogate pair D83D DC4D as two UTF-16 code units.
		keyboard.Events
			.Where(e => e.Action == KeyAction.Down && e.IsUnicode)
			.Select(e => e.Code)
			.Should()
			.ContainInConsecutiveOrder((ushort)0xD83D, (ushort)0xDC4D);
		keyboard.ReconstructTypedText().Should().Be("I 👍 it");
	}

	[Fact]
	public void A_line_feed_is_sent_as_an_enter_keypress_not_a_literal_character()
	{
		RecordingKeyboard keyboard = new();
		SendInputTextInjector injector = new(keyboard);

		injector.Inject("a\nb");

		keyboard.Events.Should().Contain(new KeyEvent(0x0D, IsUnicode: false, KeyAction.Down));
		keyboard.Events.Should().NotContain(e => e.IsUnicode && e.Code == '\n');
		keyboard.ReconstructTypedText().Should().Be("a\nb");
	}

	[Theory]
	[InlineData("a\r\nb")]
	[InlineData("a\rb")]
	public void Crlf_and_lone_cr_collapse_to_a_single_enter(string text)
	{
		RecordingKeyboard keyboard = new();
		SendInputTextInjector injector = new(keyboard);

		injector.Inject(text);

		keyboard.Events.Count(e => e is { IsUnicode: false, Code: 0x0D, Action: KeyAction.Down }).Should().Be(1);
		keyboard.ReconstructTypedText().Should().Be("a\nb");
	}

	[Fact]
	public void A_tab_is_typed_literally_as_its_code_unit()
	{
		RecordingKeyboard keyboard = new();
		SendInputTextInjector injector = new(keyboard);

		injector.Inject("a\tb");

		keyboard.Events.Should().Contain(new KeyEvent('\t', IsUnicode: true, KeyAction.Down));
		keyboard.ReconstructTypedText().Should().Be("a\tb");
	}

	[Theory]
	[InlineData("")]
	[InlineData(null)]
	public void Empty_or_null_input_synthesizes_nothing(string? text)
	{
		RecordingKeyboard keyboard = new();
		SendInputTextInjector injector = new(keyboard);

		injector.Inject(text!);

		keyboard.SendCalls.Should().Be(0);
		keyboard.Events.Should().BeEmpty();
	}
}
