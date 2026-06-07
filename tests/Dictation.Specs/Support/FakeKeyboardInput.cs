// A recording stand-in for the Win32 SendInput seam, used by the text-delivery specs. It captures the
// synthetic key events the real injector produces and reconstructs the text a focused field would see,
// so a scenario can assert "the focused window receives these characters" without synthesizing real
// input. Down events carry the content; Up events mirror them and are ignored when rebuilding the text.

using System.Text;
using Infrastructure.TextDelivery;

namespace Dictation.Specs.Support;

public sealed class FakeKeyboardInput : IKeyboardInput
{
	private const ushort VkReturn = 0x0D;

	public List<KeyEvent> Events { get; } = [];

	public void Send(IReadOnlyList<KeyEvent> events) => Events.AddRange(events);

	// The characters a focused field would receive: Unicode events as their code unit (surrogate pairs
	// recombine when the builder is materialized), Enter as a newline.
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
