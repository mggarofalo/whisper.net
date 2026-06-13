// FluentValidation rules for DownloadModelCommand, run by the ValidationBehavior pipeline
// before the handler. The id must name a model in the on-device catalog — the set we can actually
// download — so an unknown id is short-circuited before any network call.

using Application.Ports;
using FluentValidation;

namespace Application.Models;

public sealed class DownloadModelCommandValidator : AbstractValidator<DownloadModelCommand>
{
	public DownloadModelCommandValidator(IModelCatalog catalog)
	{
		RuleFor(command => command.ModelId)
			.NotEmpty()
			.Must(id => catalog.Find(id) is not null)
			.WithMessage("Unknown model id '{PropertyValue}'.");
	}
}
