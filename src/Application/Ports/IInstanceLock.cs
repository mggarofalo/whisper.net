// Port for the single-instance lock (WHISPER-25): the OS-global mutual exclusion that lets exactly one
// instance own the audio device, hotkey hooks, and tray icon. Implemented in Infrastructure with a
// named Mutex (current-user, no elevation); faked in specs. TryAcquire returns whether THIS process
// became the sole owner; Release frees it on graceful shutdown so a later launch can become the owner.

namespace Application.Ports;

public interface IInstanceLock
{
	/// <summary>Attempts to become the sole instance. Returns true if acquired, false if already held.</summary>
	bool TryAcquire();

	/// <summary>Releases the lock so a subsequent launch can become the sole instance.</summary>
	void Release();
}
