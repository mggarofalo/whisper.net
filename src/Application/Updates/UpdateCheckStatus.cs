// What a user-initiated update check found. NotInstalled: this build cannot self-update, so nothing was
// checked (no network). UpToDate / Available: the channel answered. Failed: the check could not complete.

namespace Application.Updates;

public enum UpdateCheckStatus
{
	NotInstalled,
	UpToDate,
	Available,
	Failed,
}
