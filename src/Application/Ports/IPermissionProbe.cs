// Port for checking whether the OS-level permissions the app needs to deliver text are in place
// (WHISPER-51): the global keyboard hook (SharpHook) and synthetic input (SendInput). Onboarding checks
// this and lets the user re-attempt if it is not yet granted. Implemented in Infrastructure; faked in
// specs so the grant/deny-and-retry flow can be driven without touching the OS.

namespace Application.Ports;

public interface IPermissionProbe
{
	/// <summary>Returns whether the input permissions required for dictation delivery are currently granted.</summary>
	bool HasRequiredInputPermissions();
}
