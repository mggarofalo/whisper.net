// The result of an auto-update attempt (WHISPER-29), so the caller (a hosted service today) and the
// specs can reason about what happened without inspecting logs. Disabled: the opt-in switch is off, so
// nothing was checked (no egress). UpToDate: checked, already current. Updated: a newer release was
// downloaded and staged to apply. Failed: the check/download failed and the app keeps running unchanged.

namespace Application.Updates;

public enum UpdateOutcome
{
	Disabled,
	UpToDate,
	Updated,
	Failed,
}
