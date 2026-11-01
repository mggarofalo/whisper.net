// The outcome of InstallUpdateCommand: either the update is staged and the app is shutting down to apply
// it, or the install failed (with a readable reason) and the app keeps running on the current version.

namespace Application.Updates;

public sealed record UpdateInstallResult(bool Restarting, string? Error = null);
