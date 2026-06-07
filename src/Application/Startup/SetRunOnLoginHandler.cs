// Handles SetRunOnLoginCommand: enables or disables launch-at-login through the IStartupRegistration
// port. Both port operations are idempotent, so repeated commands never duplicate or orphan the
// registration.

using Application.Interfaces;
using Application.Ports;

namespace Application.Startup;

public sealed class SetRunOnLoginHandler(IStartupRegistration registration)
	: ICommandHandler<SetRunOnLoginCommand, Mediator.Unit>
{
	public ValueTask<Mediator.Unit> Handle(SetRunOnLoginCommand command, CancellationToken cancellationToken)
	{
		if (command.Enabled)
		{
			registration.Enable();
		}
		else
		{
			registration.Disable();
		}

		return ValueTask.FromResult(Mediator.Unit.Value);
	}
}
