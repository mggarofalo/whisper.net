// Validates a post-process configuration change (WHISPER-41) in the ValidationBehavior pipeline, so an
// invalid configuration is reported clearly and never reaches the live holder: the default transform
// must be a known transform, and an enabled rephrase endpoint must be loopback-only.

using FluentValidation;

namespace Application.PostProcessing;

public sealed class ConfigurePostProcessingCommandValidator : AbstractValidator<ConfigurePostProcessingCommand>
{
	public ConfigurePostProcessingCommandValidator()
	{
		When(command => !string.IsNullOrWhiteSpace(command.DefaultTransform), () =>
			RuleFor(command => command.DefaultTransform)
				.Must(name => KnownTransforms.IsKnown(name!))
				.WithMessage("Unknown output transform '{PropertyValue}'."));

		When(command => command.RephraseEnabled, () =>
			RuleFor(command => command.RephraseEndpoint)
				.Must(BeLoopback)
				.WithMessage("AI rephrase endpoint must be localhost only."));
	}

	private static bool BeLoopback(string endpoint) =>
		Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri) && uri.IsLoopback;
}
