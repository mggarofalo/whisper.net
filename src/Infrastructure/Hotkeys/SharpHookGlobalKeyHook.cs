// The real IGlobalKeyHook: a thin wrapper over SharpHook's EventLoopGlobalHook (libuiohook), which
// dispatches its handlers on a dedicated event-loop thread. Deliberately minimal glue — it only
// forwards the raw KeyCode out of SharpHook's event args and relays Run/Stop/Dispose — so the
// testable coordination lives above it in EventLoopHotkeyListener. This type is the one piece that
// touches the native hook and so is exercised by manual smoke, not the headless specs.

using SharpHook;
using SharpHook.Data;

namespace Infrastructure.Hotkeys;

public sealed class SharpHookGlobalKeyHook : IGlobalKeyHook
{
	private readonly IGlobalHook _hook = new EventLoopGlobalHook();

	public event EventHandler<KeyCode>? KeyPressed;
	public event EventHandler<KeyCode>? KeyReleased;

	public SharpHookGlobalKeyHook()
	{
		_hook.KeyPressed += (_, e) => KeyPressed?.Invoke(this, e.Data.KeyCode);
		_hook.KeyReleased += (_, e) => KeyReleased?.Invoke(this, e.Data.KeyCode);
	}

	public void Run() => _hook.Run();

	public void Stop() => _hook.Stop();

	public void Dispose() => _hook.Dispose();
}
