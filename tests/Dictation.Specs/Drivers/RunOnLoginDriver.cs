// Drives the run-on-login scenarios through the REAL Mediator pipeline (SetRunOnLoginCommand
// + GetRunOnLoginQuery and their handlers), substituting only the IStartupRegistration port with an
// in-memory fake. It asserts at the port boundary (the registration's real state) and through the query
// (so both the command and query handlers are exercised).
//
// It also drives the REAL StartupRegistrationHealingService for the launch-at-login upkeep scenarios,
// constructed directly here because the scenario container composes the inner layers without running a
// Generic Host.

using Application.Startup;
using AwesomeAssertions;
using Dictation.Specs.Support;
using Logic.AppManagement.Lifecycle;
using Mediator;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dictation.Specs.Drivers;

public sealed class RunOnLoginDriver(IMediator mediator, FakeStartupRegistration registration)
{
	public void GivenCurrentlyEnabled(bool enabled) => registration.SetInitialState(enabled);

	// A registration written by an earlier install: the command names another executable, which may or may
	// not still be on disk.
	public void GivenRegisteredByAnotherInstall(bool targetExists) =>
		registration.SetRegisteredCommand("\"D:\\Old\\Whisper.Net\\current\\Presentation.exe\"", targetExists);

	public async Task SetEnabled(bool enabled) => await mediator.Send(new SetRunOnLoginCommand(enabled));

	// The upkeep the host performs on every launch.
	public Task AppStarts() =>
		new StartupRegistrationHealingService(registration, NullLogger<StartupRegistrationHealingService>.Instance)
			.StartAsync(CancellationToken.None);

	public async Task AssertRegistration(bool present)
	{
		bool reported = await mediator.Send(new GetRunOnLoginQuery());
		reported.Should().Be(present, "the query should reflect the real registration state");
		registration.IsEnabled().Should().Be(present, "the command should have updated the registration");
	}

	public void AssertRegistrationPointsAtThisInstall()
	{
		registration.RegisteredCommand.Should().Be(
			registration.ExpectedCommand, "a stale entry must be repointed at the install that is running");
		registration.RegisteredTargetExists.Should().BeTrue("the repointed target is this install's executable");
	}

	public void AssertRegistrationUnchangedFromAnotherInstall() =>
		registration.RegisteredCommand.Should().NotBe(registration.ExpectedCommand);
}
