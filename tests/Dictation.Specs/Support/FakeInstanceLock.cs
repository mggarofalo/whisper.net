// In-memory stand-in for the OS-global single-instance lock (the IInstanceLock port). A single shared
// instance models the one named mutex two processes contend for: the first TryAcquire wins, a second
// fails until the owner Releases. Lets the @WHISPER-25 coordinator be driven for real with no real OS
// mutex.

using Application.Ports;

namespace Dictation.Specs.Support;

public sealed class FakeInstanceLock : IInstanceLock
{
	private bool _held;

	public bool TryAcquire()
	{
		if (_held)
		{
			return false;
		}

		_held = true;
		return true;
	}

	public void Release() => _held = false;
}
