// IPermissionProbe for Windows (WHISPER-51). Unlike macOS — which gates synthetic input and global key
// hooks behind an explicit Accessibility grant — Windows lets a normal desktop process call SendInput
// and install a low-level keyboard hook for its own interactive session without a separate permission
// prompt. So the required input permissions are effectively always present here; the probe reports that,
// and the onboarding permission step is a confirmation/check rather than an OS authorization request.
// Kept behind the port so onboarding's request/re-attempt flow stays platform-agnostic and testable.

using Application.Ports;

namespace Infrastructure.Permissions;

public sealed class InputPermissionProbe : IPermissionProbe
{
	public bool HasRequiredInputPermissions() => true;
}
