// The outcome of resolving a stored device selection against the devices currently available: which
// device id to actually capture from, whether the selection is following the OS default (so it should
// hot-swap when the default changes), and whether a pinned device was missing and silently replaced
// by the default (a substitution the app surfaces to the user).

namespace Domain.Audio;

public sealed record DeviceResolution(string? DeviceId, bool FollowsDefault, bool Substituted);
