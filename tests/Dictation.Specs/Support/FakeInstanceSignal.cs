// In-memory stand-in for the cross-process activation channel (the IInstanceSignal port). A single
// shared instance models the one named signal: the primary Listens (subscribing via the event), and a
// second process's Signal raises ActivationRequested synchronously on the primary — exactly the
// cross-process hop the real named EventWaitHandle performs, with no OS handle.

using Application.Ports;

namespace Dictation.Specs.Support;

public sealed class FakeInstanceSignal : IInstanceSignal
{
	public event EventHandler? ActivationRequested;

	public void Listen()
	{
	}

	public void Signal() => ActivationRequested?.Invoke(this, EventArgs.Empty);

	public void Dispose()
	{
	}
}
