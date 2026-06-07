// FluentValidation rules for RecordTranscriptionCommand, run by the ValidationBehavior pipeline before
// the handler: the recognized text must be non-empty and the timestamp must be set. Pure (no I/O).

using FluentValidation;

namespace Application.History;

public sealed class RecordTranscriptionCommandValidator : AbstractValidator<RecordTranscriptionCommand>
{
	public RecordTranscriptionCommandValidator()
	{
		RuleFor(command => command.Text).NotEmpty();

		RuleFor(command => command.CreatedAt)
			.Must(timestamp => timestamp > DateTimeOffset.MinValue)
			.WithMessage("A valid transcription timestamp is required.");

		RuleFor(command => command.Duration)
			.GreaterThanOrEqualTo(TimeSpan.Zero)
			.WithMessage("Audio duration must not be negative.");
	}
}
