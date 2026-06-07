// Verifies the ValidationBehavior pipeline (WHISPER-55): a request whose validator fails never
// reaches its handler and surfaces a validation failure; a request that passes (or has no validator)
// flows through to the handler. The `next` delegate stands in for the handler, so "next was not
// invoked" is exactly "the handler did not run". Full IMediator dispatch is exercised in WHISPER-58.

using Application.Behaviors;
using FluentValidation;
using Xunit;

namespace Application.Tests;

public sealed class ValidationBehaviorTests
{
	// A minimal command + validator used only by these tests. Fully qualified because the Mediator
	// package also ships an ICommand<T>.
	private sealed record SampleCommand(string Value) : Application.Interfaces.ICommand<string>;

	private sealed class NonEmptyValueValidator : AbstractValidator<SampleCommand>
	{
		public NonEmptyValueValidator() => RuleFor(c => c.Value).NotEmpty();
	}

	private static ValidationBehavior<SampleCommand, string> Behavior(params IValidator<SampleCommand>[] validators) =>
		new(validators);

	[Fact]
	public async Task Passes_through_to_handler_when_no_validators_registered()
	{
		bool handlerInvoked = false;
		ValueTask<string> Next(SampleCommand _, CancellationToken __)
		{
			handlerInvoked = true;
			return ValueTask.FromResult("handled");
		}

		string result = await Behavior().Handle(new SampleCommand(""), Next, CancellationToken.None);

		Assert.True(handlerInvoked);
		Assert.Equal("handled", result);
	}

	[Fact]
	public async Task Reaches_handler_when_validation_passes()
	{
		bool handlerInvoked = false;
		ValueTask<string> Next(SampleCommand _, CancellationToken __)
		{
			handlerInvoked = true;
			return ValueTask.FromResult("handled");
		}

		string result = await Behavior(new NonEmptyValueValidator())
			.Handle(new SampleCommand("hello"), Next, CancellationToken.None);

		Assert.True(handlerInvoked);
		Assert.Equal("handled", result);
	}

	[Fact]
	public async Task Rejects_before_handler_when_validation_fails()
	{
		bool handlerInvoked = false;
		ValueTask<string> Next(SampleCommand _, CancellationToken __)
		{
			handlerInvoked = true;
			return ValueTask.FromResult("handled");
		}

		ValidationException exception = await Assert.ThrowsAsync<ValidationException>(() =>
			Behavior(new NonEmptyValueValidator())
				.Handle(new SampleCommand(""), Next, CancellationToken.None)
				.AsTask());

		Assert.False(handlerInvoked);
		Assert.NotEmpty(exception.Errors);
	}
}
