// FluentValidation rules for InstallUpdateCommand, run by the ValidationBehavior pipeline before the
// handler: there must be a version to install.

using FluentValidation;

namespace Application.Updates;

public sealed class InstallUpdateCommandValidator : AbstractValidator<InstallUpdateCommand>
{
	public InstallUpdateCommandValidator() => RuleFor(command => command.Version).NotEmpty();
}
