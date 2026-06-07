// Handler marker for an IQuery<TResponse>. Derives from Mediator's IRequestHandler so the
// source generator wires it up, while letting handlers declare intent as query handlers.

using Mediator;

namespace Application.Interfaces;

public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, TResponse>
	where TQuery : IQuery<TResponse>
{
}
