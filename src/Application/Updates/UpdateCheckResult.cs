// The answer to a user-initiated update check: the status, the newer version when one is available, and a
// readable reason when the check failed.

namespace Application.Updates;

public sealed record UpdateCheckResult(UpdateCheckStatus Status, string? Version = null, string? Error = null);
