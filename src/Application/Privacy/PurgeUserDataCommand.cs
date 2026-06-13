// CQRS command for the user-initiated data purge: clears the transcript history and the
// audit log from disk. Returns Unit. There is nothing to validate — purge is unconditional — so it has
// no validator and passes straight through the pipeline to the handler.

using Application.Interfaces;

namespace Application.Privacy;

public sealed record PurgeUserDataCommand : ICommand<Mediator.Unit>;
