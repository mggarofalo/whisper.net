// The IInstanceSignal adapter: cross-process activation over a named EventWaitHandle in the current
// user's session namespace (no elevation). The primary instance Listen()s — creating the named event
// and waking a background thread that raises ActivationRequested when it is set. A second launch
// Signal()s by opening that named event and setting it, so the running instance surfaces. Periodic
// timeout polling lets Dispose stop the listener even when no signal arrives. Windows-only OS
// device-glue, verified by smoke; the coordination it backs is spec-driven over a fake seam.

using System.Runtime.Versioning;
using Application.Ports;

namespace Infrastructure.Lifecycle;

[SupportedOSPlatform("windows")]
public sealed class EventWaitHandleInstanceSignal(string name) : IInstanceSignal
{
	private EventWaitHandle? _handle;
	private Thread? _listener;
	private volatile bool _listening;

	public event EventHandler? ActivationRequested;

	public void Listen()
	{
		_handle = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, name);
		_listening = true;
		_listener = new Thread(WaitLoop) { IsBackground = true, Name = "single-instance-activation" };
		_listener.Start();
	}

	public void Signal()
	{
		// Open the primary's named event (if any) and set it; absent a primary there is nothing to signal.
		if (EventWaitHandle.TryOpenExisting(name, out EventWaitHandle? handle))
		{
			using (handle)
			{
				handle.Set();
			}
		}
	}

	private void WaitLoop()
	{
		// Wake periodically so Dispose can stop the loop even when no activation arrives.
		while (_listening)
		{
			if (_handle!.WaitOne(TimeSpan.FromMilliseconds(250)) && _listening)
			{
				ActivationRequested?.Invoke(this, EventArgs.Empty);
			}
		}
	}

	public void Dispose()
	{
		_listening = false;
		_listener?.Join(TimeSpan.FromSeconds(1));
		_handle?.Dispose();
	}
}
