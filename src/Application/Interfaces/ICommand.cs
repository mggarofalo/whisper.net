// Marker for a CQRS command — a request that changes state and returns a result. Built on the
// source-generated Mediator's IRequest<T> so the generator discovers and dispatches handlers, while
// giving the codebase its own command/query vocabulary independent of the Mediator package.

using Mediator;

namespace Application.Interfaces;

public interface ICommand<out TResponse> : IRequest<TResponse>
{
}
