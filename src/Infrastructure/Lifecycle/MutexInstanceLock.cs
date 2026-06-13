// The IInstanceLock adapter: a named Mutex in the current user's session namespace (no "Global\" prefix,
// so no elevation and no cross-session contention). TryAcquire takes ownership without blocking; if the
// previous owner crashed without releasing, the abandoned mutex is treated as acquired. Release frees it
// on graceful shutdown. Windows-only by nature; annotated accordingly (Infrastructure targets portable
// net10.0). This is OS device-glue verified by smoke — the single-instance coordination it backs is
// driven for real over a fake seam in the instance-lock specs.

using System.Runtime.Versioning;
using Application.Ports;

namespace Infrastructure.Lifecycle;

[SupportedOSPlatform("windows")]
public sealed class MutexInstanceLock(string name) : IInstanceLock, IDisposable
{
	private Mutex? _mutex;
	private bool _owns;

	public bool TryAcquire()
	{
		_mutex ??= new Mutex(initiallyOwned: false, name);

		try
		{
			_owns = _mutex.WaitOne(TimeSpan.Zero);
		}
		catch (AbandonedMutexException)
		{
			// The previous owner exited without releasing; ownership has passed to us.
			_owns = true;
		}

		return _owns;
	}

	public void Release()
	{
		if (_owns)
		{
			_mutex!.ReleaseMutex();
			_owns = false;
		}
	}

	public void Dispose()
	{
		Release();
		_mutex?.Dispose();
	}
}
