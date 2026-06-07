// Port for delivering text into whatever field currently has focus. Implemented in Infrastructure by
// the SendInput adapter; faked in the BDD specs so delivery can be asserted at the boundary.

namespace Application.Ports;

public interface ITextInjector
{
	void Inject(string text);
}
