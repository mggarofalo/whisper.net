// FluentValidation rules for SwitchActiveModelCommand, run by the ValidationBehavior
// pipeline before the handler. The id must name a model in the on-device catalog, so an unknown id is
// rejected before the lifecycle is asked to switch.

using Application.Ports;
using FluentValidation;

namespace Application.Models;

public sealed class SwitchActiveModelCommandValidator : AbstractValidator<SwitchActiveModelCommand>
{
	public SwitchActiveModelCommandValidator(IModelCatalog catalog)
	{
		RuleFor(command => command.ModelId)
			.NotEmpty()
			.Must(id => catalog.Find(id) is not null)
			.WithMessage("Unknown model id '{PropertyValue}'.");
	}
}
