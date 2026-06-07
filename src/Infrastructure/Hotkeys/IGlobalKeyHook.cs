// Internal seam over SharpHook's native global hook. Splitting the libuiohook glue out behind this
// interface lets EventLoopHotkeyListener's coordination logic — owning the dedicated pump thread,
// translating raw key codes to domain keys, tracking the live modifier set, surviving a failed hook
// start — run and be tested without a real OS hook: the BDD specs and unit tests feed a fake, while
// SharpHookGlobalKeyHook wraps the real EventLoopGlobalHook. The seam speaks SharpHook's KeyCode (the
// raw currency); turning that into a Domain key is the listener's job, above this line.

using SharpHook.Data;

namespace Infrastructure.Hotkeys;

public interface IGlobalKeyHook : IDisposable
{
	/// <summary>Raised for each raw key-down the OS reports.</summary>
	event EventHandler<KeyCode>? KeyPressed;

	/// <summary>Raised for each raw key-up the OS reports.</summary>
	event EventHandler<KeyCode>? KeyReleased;

	/// <summary>
	/// Runs the hook's event loop, blocking the calling thread until <see cref="Stop"/> or
	/// <see cref="IDisposable.Dispose"/> is called. The caller is expected to invoke this on a
	/// dedicated thread.
	/// </summary>
	void Run();

	/// <summary>Stops the event loop so a blocked <see cref="Run"/> returns; the hook can be re-run.</summary>
	void Stop();
}
