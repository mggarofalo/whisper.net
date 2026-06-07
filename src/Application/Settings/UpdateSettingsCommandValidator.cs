// FluentValidation rules for UpdateSettingsCommand, run by the ValidationBehavior pipeline before the
// handler. Enforces a known model id, a non-empty parseable hotkey binding, and a sane silence
// threshold — so an invalid update is short-circuited and never written. Rules are pure (no I/O).

using Domain;
using Domain.Settings;
using FluentValidation;

namespace Application.Settings;

public sealed class UpdateSettingsCommandValidator : AbstractValidator<UpdateSettingsCommand>
{
	// Upper bound on the trailing-silence threshold; beyond a minute it is almost certainly a mistake.
	private const int MaxSilenceThresholdMs = 60_000;

	public UpdateSettingsCommandValidator()
	{
		RuleFor(command => command.Settings).NotNull();

		When(command => command.Settings is not null, () =>
		{
			RuleFor(command => command.Settings.ModelId)
				.NotEmpty()
				.Must(KnownModels.IsKnown)
				.WithMessage("Unknown model id '{PropertyValue}'.");

			RuleFor(command => command.Settings.Hotkey)
				.NotEmpty()
				.Must(BeAParseableHotkey)
				.WithMessage("Invalid hotkey binding '{PropertyValue}'.");

			RuleFor(command => command.Settings.SilenceThresholdMs)
				.InclusiveBetween(0, MaxSilenceThresholdMs);
		});
	}

	// True when the chord parses to a valid HotkeyBinding. Pure: swallows the domain rejection rather
	// than letting it escape the validator.
	private static bool BeAParseableHotkey(string chord)
	{
		try
		{
			HotkeyBinding.Parse(chord);
			return true;
		}
		catch (DomainException)
		{
			return false;
		}
	}
}
