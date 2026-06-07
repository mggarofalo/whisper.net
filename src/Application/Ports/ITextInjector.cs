// Port for delivering text into whatever field currently has focus. Implemented in Infrastructure by
// the SendInput adapter; faked in the BDD specs so delivery can be asserted at the boundary.

namespace Application.Ports;

/// <summary>
/// Delivers text into the field that currently has focus (the final step of the dictation pipeline).
/// </summary>
/// <remarks>Synchronous — a fast OS call (SendInput). Must run on a thread that can synthesize input.</remarks>
public interface ITextInjector
{
	/// <summary>Injects the given text into the focused field.</summary>
	void Inject(string text);
}
