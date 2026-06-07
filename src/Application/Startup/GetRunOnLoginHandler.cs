// Handles GetRunOnLoginQuery: reads the current launch-at-login registration through the
// IStartupRegistration port. Pure orchestration — the registry source of truth lives behind the port.

using Application.Interfaces;
using Application.Ports;

namespace Application.Startup;

public sealed class GetRunOnLoginHandler(IStartupRegistration registration)
	: IQueryHandler<GetRunOnLoginQuery, bool>
{
	public ValueTask<bool> Handle(GetRunOnLoginQuery query, CancellationToken cancellationToken) =>
		ValueTask.FromResult(registration.IsEnabled());
}
