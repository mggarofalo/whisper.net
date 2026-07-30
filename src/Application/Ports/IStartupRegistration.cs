// Port for the OS "launch at user login" registration. Implemented in Infrastructure
// against the current-user registry Run key (no elevation); faked in specs. Deliberately synchronous:
// the underlying registry operations are fast, local, and have no async API worth surfacing.
//
// IsEnabled reads the real registration so the toggle never drifts from reality; Enable and Disable are
// idempotent — enabling when already enabled (or disabling when absent) leaves a single, correct entry
// with no duplicates or orphans.
//
// RegisteredCommand and ExpectedCommand exist because "an entry exists" is NOT the same as "the app will
// actually start at login": an entry written by an older install keeps pointing at an executable that a
// reinstall, a move, or an update has since removed, and Windows silently does nothing at login while the
// toggle still reads as on. Exposing both commands lets the app compare them, heal the drift, and report
// it in diagnostics instead of quietly failing to launch.

namespace Application.Ports;

public interface IStartupRegistration
{
	/// <summary>Returns whether the app is currently registered to launch at user login.</summary>
	bool IsEnabled();

	/// <summary>Registers the app to launch at login using its current executable path. Idempotent.</summary>
	void Enable();

	/// <summary>Removes any launch-at-login registration for the app. Idempotent.</summary>
	void Disable();

	/// <summary>The login command currently registered with the OS, or null when no registration exists.</summary>
	string? RegisteredCommand { get; }

	/// <summary>The login command this installation would register — what <see cref="Enable"/> writes.</summary>
	string ExpectedCommand { get; }

	/// <summary>Whether the executable named by <see cref="RegisteredCommand"/> still exists on disk.
	/// False when nothing is registered, so callers must check <see cref="IsEnabled"/> first.</summary>
	bool RegisteredTargetExists { get; }
}
