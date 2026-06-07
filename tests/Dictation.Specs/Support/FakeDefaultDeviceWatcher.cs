// A controllable stand-in for the WASAPI notification client: lets the @WHISPER-13 hot-swap scenario
// simulate the OS switching the default capture device by raising DefaultChanged on demand.

using Application.Ports;

namespace Dictation.Specs.Support;

public sealed class FakeDefaultDeviceWatcher : IDefaultDeviceWatcher
{
	public event EventHandler<DefaultDeviceChangedEventArgs>? DefaultChanged;

	public bool IsListening { get; private set; }

	public void Start() => IsListening = true;

	public void Stop() => IsListening = false;

	public void RaiseDefaultChanged(string? newDefaultId) =>
		DefaultChanged?.Invoke(this, new DefaultDeviceChangedEventArgs(newDefaultId));

	public void Dispose()
	{
	}
}
