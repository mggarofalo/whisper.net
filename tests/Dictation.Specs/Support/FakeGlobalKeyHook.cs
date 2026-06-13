// A controllable stand-in for the native global hook, implementing the Infrastructure hotkey seam. It
// lets the scenarios drive the REAL EventLoopHotkeyListener without an OS hook: the test
// decides which raw key codes arrive and when, while Run blocks a pump thread exactly as the real
// libuiohook loop does, returning only when the listener stops or disposes it. Only the native glue is
// faked here — the threading, translation, and modifier tracking under test are real.

using Infrastructure.Hotkeys;
using SharpHook.Data;

namespace Dictation.Specs.Support;

public sealed class FakeGlobalKeyHook : IGlobalKeyHook
{
	private readonly object _gate = new();
	private bool _stopRequested;

	public bool IsRunning { get; private set; }
	public bool Disposed { get; private set; }

	public event EventHandler<KeyCode>? KeyPressed;
	public event EventHandler<KeyCode>? KeyReleased;

	// Block like the native event loop until the listener asks us to stop. If a stop is requested
	// before we ever start, we return at once rather than waiting forever.
	public void Run()
	{
		lock (_gate)
		{
			IsRunning = true;
			while (!_stopRequested)
			{
				Monitor.Wait(_gate);
			}

			IsRunning = false;
		}
	}

	public void Stop()
	{
		lock (_gate)
		{
			_stopRequested = true;
			Monitor.PulseAll(_gate);
		}
	}

	public void Dispose()
	{
		Disposed = true;
		Stop();
	}

	// Deliver a raw key-down/up as if the OS produced it.
	public void Press(KeyCode code) => KeyPressed?.Invoke(this, code);

	public void Release(KeyCode code) => KeyReleased?.Invoke(this, code);
}
