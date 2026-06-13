// Port for the OS "launch at user login" registration. Implemented in Infrastructure
// against the current-user registry Run key (no elevation); faked in specs. Deliberately synchronous:
// the underlying registry operations are fast, local, and have no async API worth surfacing.
//
// IsEnabled reads the real registration so the toggle never drifts from reality; Enable and Disable are
// idempotent — enabling when already enabled (or disabling when absent) leaves a single, correct entry
// with no duplicates or orphans.

namespace Application.Ports;

public interface IStartupRegistration
{
	/// <summary>Returns whether the app is currently registered to launch at user login.</summary>
	bool IsEnabled();

	/// <summary>Registers the app to launch at login using its current executable path. Idempotent.</summary>
	void Enable();

	/// <summary>Removes any launch-at-login registration for the app. Idempotent.</summary>
	void Disable();
}
