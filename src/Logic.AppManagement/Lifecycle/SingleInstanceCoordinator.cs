// Single-instance coordination, kept out of Presentation/Infrastructure so it can be
// driven for real in specs. On startup TryStartAsPrimary tries to acquire the OS-global lock: if it
// wins, this process is the primary — it listens for activation requests and surfaces the window (via
// the IShellPresenter seam) whenever a later launch signals it. If the lock is already held, it signals
// the existing instance to surface and returns false so the second process exits without starting a
// host. ReleasePrimary frees the lock on graceful shutdown so a subsequent launch becomes the sole
// instance. All OS specifics (the named mutex and the cross-process signal) live behind the two ports.

using Application.Ports;

namespace Logic.AppManagement.Lifecycle;

public sealed class SingleInstanceCoordinator(
	IInstanceLock instanceLock,
	IInstanceSignal signal,
	IShellPresenter shell) : IDisposable
{
	private bool _isPrimary;

	/// <summary>
	/// Attempts to start as the sole instance. Returns true if this process acquired the lock and should
	/// start the host; false if another instance is already running (which has been signalled to surface)
	/// and this process should exit without starting a host.
	/// </summary>
	public bool TryStartAsPrimary()
	{
		if (instanceLock.TryAcquire())
		{
			_isPrimary = true;
			signal.ActivationRequested += OnActivationRequested;
			signal.Listen();
			return true;
		}

		// Another instance owns the lock: ask it to surface, then bow out.
		signal.Signal();
		return false;
	}

	/// <summary>Releases the lock on graceful shutdown so a later launch can become the sole instance.</summary>
	public void ReleasePrimary()
	{
		if (!_isPrimary)
		{
			return;
		}

		signal.ActivationRequested -= OnActivationRequested;
		instanceLock.Release();
		_isPrimary = false;
	}

	private void OnActivationRequested(object? sender, EventArgs e) => shell.ShowSettings();

	public void Dispose() => ReleasePrimary();
}
