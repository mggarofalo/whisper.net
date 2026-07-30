// Launch-at-login diagnostic: reports whether the app will actually start at user login. "An entry
// exists" is not enough — an entry left behind by an earlier install points at an executable a reinstall
// or a move has removed, and Windows then does nothing at login while the toggle still reads as on. So the
// check compares the registered command against the one this installation expects and confirms its target
// is still on disk. Launch at login being switched off PASSES: it is a legitimate choice, and reporting it
// as a warning would put permanent noise in the health report of every user who prefers to start the app
// themselves. Only a registration that cannot work is a finding.

using Application.Diagnostics;
using Application.Ports;

namespace Logic.AppManagement.Diagnostics;

public sealed class StartupRegistrationCheck(IStartupRegistration registration) : IDiagnosticCheck
{
	public string Name => "Startup";

	public ValueTask<DiagnosticResult> RunAsync(CancellationToken cancellationToken)
	{
		if (!registration.IsEnabled())
		{
			return ValueTask.FromResult(new DiagnosticResult(
				Name, DiagnosticStatus.Pass, "Whisper is not set to start at login."));
		}

		string? registered = registration.RegisteredCommand;
		if (!registration.RegisteredTargetExists)
		{
			return ValueTask.FromResult(new DiagnosticResult(
				Name,
				DiagnosticStatus.Fail,
				$"Start at login points at {registered}, which no longer exists — Whisper will not start at login."));
		}

		if (!string.Equals(registered, registration.ExpectedCommand, StringComparison.OrdinalIgnoreCase))
		{
			return ValueTask.FromResult(new DiagnosticResult(
				Name,
				DiagnosticStatus.Warn,
				$"Start at login points at another install ({registered}); this one is {registration.ExpectedCommand}."));
		}

		return ValueTask.FromResult(new DiagnosticResult(
			Name, DiagnosticStatus.Pass, $"Whisper starts at login via {registered}."));
	}
}
