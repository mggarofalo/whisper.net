// Drives the run-on-login scenarios through the REAL Mediator pipeline (SetRunOnLoginCommand
// + GetRunOnLoginQuery and their handlers), substituting only the IStartupRegistration port with an
// in-memory fake. It asserts at the port boundary (the registration's real state) and through the query
// (so both the command and query handlers are exercised).

using Application.Startup;
using AwesomeAssertions;
using Dictation.Specs.Support;
using Mediator;

namespace Dictation.Specs.Drivers;

public sealed class RunOnLoginDriver(IMediator mediator, FakeStartupRegistration registration)
{
	public void GivenCurrentlyEnabled(bool enabled) => registration.SetInitialState(enabled);

	public async Task SetEnabled(bool enabled) => await mediator.Send(new SetRunOnLoginCommand(enabled));

	public async Task AssertRegistration(bool present)
	{
		bool reported = await mediator.Send(new GetRunOnLoginQuery());
		reported.Should().Be(present, "the query should reflect the real registration state");
		registration.IsEnabled().Should().Be(present, "the command should have updated the registration");
	}
}
