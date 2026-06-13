// A newer release the update source found on the channel: just the version, which is all
// the update policy needs to report and log. A null AvailableUpdate means the app is up to date.

namespace Application.Updates;

public sealed record AvailableUpdate(string Version);
