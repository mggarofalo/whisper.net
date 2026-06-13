// An in-memory stand-in for the OS startup registration (the IStartupRegistration port).
// Models the single source of truth a real registry Run key provides:
// enabling/disabling flips one flag, so it is inherently idempotent (no duplicates, no orphans) — the
// real registry idempotency is proven separately against an actual HKCU subkey in Infrastructure.Tests.

using Application.Ports;

namespace Dictation.Specs.Support;

public sealed class FakeStartupRegistration : IStartupRegistration
{
	private bool _enabled;

	public bool IsEnabled() => _enabled;

	public void Enable() => _enabled = true;

	public void Disable() => _enabled = false;

	// Test seam: set the starting registration state for a scenario's Given.
	public void SetInitialState(bool enabled) => _enabled = enabled;
}
