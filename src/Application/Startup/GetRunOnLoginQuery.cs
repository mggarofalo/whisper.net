// CQRS query to read whether the app is registered to launch at user login. The handler reads the real
// registration through the IStartupRegistration port, so the answer always reflects the OS source of
// truth.

using Application.Interfaces;

namespace Application.Startup;

public sealed record GetRunOnLoginQuery : IQuery<bool>;
