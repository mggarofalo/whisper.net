// The IInstanceLock adapter: a named Mutex in the current user's session namespace (no "Global\" prefix,
// so no elevation and no cross-session contention). TryAcquire takes ownership without blocking; if the
// previous owner crashed without releasing, the abandoned mutex is treated as acquired. Windows-only by
// nature; annotated accordingly (Infrastructure targets portable net10.0).
//
// A Win32 mutex is owned by the THREAD that acquired it, and ReleaseMutex from any other thread throws
// ApplicationException ("Object synchronization method was called from an unsynchronized block of code").
// Shutdown cannot promise thread affinity — the Generic Host's DisposeAsync resumes on a pool thread — so
// Release only calls ReleaseMutex on the acquiring thread. From any other thread it closes the handle
// instead, and Windows abandons the ownership when the owning thread exits; the next launch treats an
// abandoned mutex as acquired, so the outcome for a later launch is the same. Release and Dispose are
// idempotent and never throw.

using System.Runtime.Versioning;
using Application.Ports;

namespace Infrastructure.Lifecycle;

[SupportedOSPlatform("windows")]
public sealed class MutexInstanceLock(string name) : IInstanceLock, IDisposable
{
	private readonly Lock _gate = new();
	private Mutex? _mutex;
	private bool _owns;
	private int _ownerThreadId;

	public bool TryAcquire()
	{
		lock (_gate)
		{
			if (_owns)
			{
				return true;
			}

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

			if (_owns)
			{
				_ownerThreadId = Environment.CurrentManagedThreadId;
			}

			return _owns;
		}
	}

	public void Release()
	{
		lock (_gate)
		{
			if (!_owns)
			{
				return;
			}

			_owns = false;

			if (Environment.CurrentManagedThreadId == _ownerThreadId)
			{
				_mutex!.ReleaseMutex();
				return;
			}

			// Not the owning thread: ReleaseMutex would throw. Close our handle; the ownership is abandoned
			// when the owning thread exits, which a later TryAcquire treats as acquired.
			_mutex!.Dispose();
			_mutex = null;
		}
	}

	public void Dispose()
	{
		lock (_gate)
		{
			Release();
			_mutex?.Dispose();
			_mutex = null;
		}
	}
}
