// Mediator pipeline behavior that validates every request before its handler runs. It executes all
// FluentValidation validators registered for the request type and, if any fail, throws a
// ValidationException so the handler is never reached. Requests with no registered validator pass
// straight through. This is wired into the pipeline by AddApplication().

using FluentValidation;
using FluentValidation.Results;
using Mediator;

namespace Application.Behaviors;

public sealed class ValidationBehavior<TMessage, TResponse>(IEnumerable<IValidator<TMessage>> validators)
	: IPipelineBehavior<TMessage, TResponse>
	where TMessage : notnull, IMessage
{
	public async ValueTask<TResponse> Handle(
		TMessage message,
		MessageHandlerDelegate<TMessage, TResponse> next,
		CancellationToken cancellationToken)
	{
		if (!validators.Any())
		{
			return await next(message, cancellationToken);
		}

		ValidationResult[] results = await Task.WhenAll(
			validators.Select(v => v.ValidateAsync(new ValidationContext<TMessage>(message), cancellationToken)));

		List<ValidationFailure> failures = [.. results.SelectMany(r => r.Errors).Where(f => f is not null)];

		if (failures.Count > 0)
		{
			throw new ValidationException(failures);
		}

		return await next(message, cancellationToken);
	}
}
