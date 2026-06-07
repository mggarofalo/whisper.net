// Port for cross-process activation signalling (WHISPER-25): how a second launch tells the already-
// running instance to surface itself. Implemented in Infrastructure with a named EventWaitHandle
// (current-user, no elevation); faked in specs. The primary instance Listens; a second process Signals
// it, which raises ActivationRequested in the primary. Disposable because the listener owns a
// background wait.

namespace Application.Ports;

public interface IInstanceSignal : IDisposable
{
	/// <summary>Raised in the primary instance when another launch requests activation.</summary>
	event EventHandler? ActivationRequested;

	/// <summary>Begins listening for activation requests (the primary instance only).</summary>
	void Listen();

	/// <summary>Signals the existing instance to surface, from a second launch.</summary>
	void Signal();
}
