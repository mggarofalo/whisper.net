// Port for reading and writing the system clipboard. Implemented in Infrastructure; faked in specs.
// Synchronous like ITextInjector — these are fast local OS calls, not meaningfully asynchronous.

namespace Application.Ports;

/// <summary>
/// Reads and writes the system clipboard, used when delivery is configured to paste rather than
/// type, and to restore prior clipboard contents afterward.
/// </summary>
/// <remarks>Must be called on the thread the platform requires for clipboard access (the adapter's concern).</remarks>
public interface IClipboard
{
	/// <summary>Returns the current clipboard text, or <c>null</c> if it holds no text.</summary>
	string? GetText();

	/// <summary>Replaces the clipboard contents with the given text.</summary>
	void SetText(string text);
}
