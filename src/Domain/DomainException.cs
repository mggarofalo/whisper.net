// Signals that a domain invariant was violated — the single way the Domain layer rejects an
// attempt to construct or transition into an invalid state. Catching this type is how the outer
// layers (and the BDD specs) observe "the domain refused this operation" without depending on the
// specific rule that failed.

namespace Domain;

public sealed class DomainException : Exception
{
	public DomainException(string message) : base(message)
	{
	}
}
