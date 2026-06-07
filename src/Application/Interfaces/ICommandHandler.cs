// Handler marker for an ICommand<TResponse>. Derives from Mediator's IRequestHandler so the
// source generator wires it up, while letting handlers declare intent as command handlers.

using Mediator;

namespace Application.Interfaces;

public interface ICommandHandler<in TCommand, TResponse> : IRequestHandler<TCommand, TResponse>
	where TCommand : ICommand<TResponse>
{
}
