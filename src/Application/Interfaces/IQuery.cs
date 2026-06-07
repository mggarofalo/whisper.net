// Marker for a CQRS query — a request that reads state without changing it and returns a result.
// Built on the source-generated Mediator's IRequest<T> (see ICommand for the rationale).

using Mediator;

namespace Application.Interfaces;

public interface IQuery<out TResponse> : IRequest<TResponse>
{
}
