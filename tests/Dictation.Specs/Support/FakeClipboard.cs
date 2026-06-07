// An in-memory clipboard for the text-delivery specs. Its change count advances on every write — by us
// or by a simulated other process — so the paste path's "don't clobber newer content" guard can be
// exercised exactly as it would be against the real Win32 clipboard, with no global state touched.

using Application.Ports;

namespace Dictation.Specs.Support;

public sealed class FakeClipboard : IClipboard
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

	/// <summary>Simulates another process copying content — advances the change count like any write.</summary>
	public void ExternalCopy(string text) => SetText(text);

	/// <summary>Seeds the user's prior contents for a scenario (null models an empty/non-text clipboard).</summary>
	public void Seed(string? text)
	{
		Text = text;
		if (text is not null)
		{
			_changeCount++;
		}
	}
}
