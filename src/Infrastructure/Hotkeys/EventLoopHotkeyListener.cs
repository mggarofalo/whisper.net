// The IHotkeyListener adapter: the device-independent coordination of global key observation, sitting
// on top of the IGlobalKeyHook seam. It owns the contract the rest of the app depends on — pump the
// hook on a single dedicated background thread so Start never blocks, translate raw key codes to
// Domain keys, track the live modifier set so each edge carries a consistent snapshot, join the
// thread cleanly on Stop/Dispose, and survive a hook that fails to start by logging instead of
// crashing the host. All of this runs without a real hook (over a fake seam), which is how the
// hotkey-listener specs exercise it; SharpHookGlobalKeyHook supplies the real one.

using Application.Ports;
using Domain.Input;
using Microsoft.Extensions.Logging;
using SharpHook.Data;

namespace Infrastructure.Hotkeys;

public sealed class EventLoopHotkeyListener(IGlobalKeyHook hook, ILogger<EventLoopHotkeyListener> logger)
	: IHotkeyListener, IDisposable
{
	private readonly Lock _gate = new();
	private Thread? _pump;
	private bool _running;
	private KeyModifiers _modifiers;

	public event EventHandler<KeyboardKeyEventArgs>? KeyDown;
	public event EventHandler<KeyboardKeyEventArgs>? KeyUp;

	// Subscribe, reset modifier state, then spin the dedicated pump thread. A second call while
	// already running is a no-op so the OS hook is never double-pumped. Returns immediately — the
	// blocking hook loop runs on the pump thread, never the caller's.
	public void Start()
	{
		lock (_gate)
		{
			if (_running)
			{
				return;
			}

			hook.KeyPressed += OnKeyPressed;
			hook.KeyReleased += OnKeyReleased;
			_modifiers = KeyModifiers.None;
			_running = true;
			_pump = new Thread(Pump) { IsBackground = true, Name = "global-hotkey-hook" };
			_pump.Start();
		}
	}

	// Stop the loop and join the pump thread so shutdown is clean. Unsubscribing before stopping means
	// no edge raised during teardown reaches a subscriber. Idempotent.
	public void Stop()
	{
		Thread? pump;
		lock (_gate)
		{
			if (!_running)
			{
				return;
			}

			_running = false;
			hook.KeyPressed -= OnKeyPressed;
			hook.KeyReleased -= OnKeyReleased;
			hook.Stop();
			pump = _pump;
			_pump = null;
		}

		// Join outside the lock: the pump thread may still be unwinding a handler that needs the gate.
		pump?.Join();

		lock (_gate)
		{
			_modifiers = KeyModifiers.None;
		}
	}

	public void Dispose()
	{
		Stop();
		hook.Dispose();
	}

	// Run the hook's blocking event loop. A failure to start (e.g. no hook permission) is logged and
	// surfaced as a stopped loop rather than an exception that takes down the host.
	private void Pump()
	{
		try
		{
			hook.Run();
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Global hotkey hook stopped unexpectedly; hotkeys are disabled until restart.");
		}
	}

	private void OnKeyPressed(object? sender, KeyCode code)
	{
		KeyboardKey key = SharpHookKeyTranslator.Translate(code);
		KeyModifiers snapshot;
		lock (_gate)
		{
			_modifiers |= key.AsModifier();
			snapshot = _modifiers;
		}

		KeyDown?.Invoke(this, new KeyboardKeyEventArgs(key, snapshot));
	}

	private void OnKeyReleased(object? sender, KeyCode code)
	{
		KeyboardKey key = SharpHookKeyTranslator.Translate(code);
		KeyModifiers snapshot;
		lock (_gate)
		{
			_modifiers &= ~key.AsModifier();
			snapshot = _modifiers;
		}

		KeyUp?.Invoke(this, new KeyboardKeyEventArgs(key, snapshot));
	}
}
