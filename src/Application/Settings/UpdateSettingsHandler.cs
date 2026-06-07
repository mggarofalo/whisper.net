// Handles UpdateSettingsCommand: maps the validated DTO to the domain settings and persists it via the
// ISettingsStore port. The command has already passed the ValidationBehavior pipeline, so the handler
// trusts its input and does not re-validate.

using Application.Interfaces;
using Application.Ports;
using Domain.Settings;

namespace Application.Settings;

public sealed class UpdateSettingsHandler(ISettingsStore store, SettingsMapper mapper)
	: ICommandHandler<UpdateSettingsCommand, Mediator.Unit>
{
	public async ValueTask<Mediator.Unit> Handle(UpdateSettingsCommand command, CancellationToken cancellationToken)
	{
		AppSettings settings = mapper.ToDomain(command.Settings);
		await store.SaveAsync(settings, cancellationToken);
		return Mediator.Unit.Value;
	}
}
