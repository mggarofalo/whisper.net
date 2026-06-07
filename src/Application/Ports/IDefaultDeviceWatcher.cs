// Port for the WASAPI notification client: raises an event when the OS default capture device
// changes, so a selection that follows the default can hot-swap without a restart. Implemented in
// Infrastructure over NAudio's MMNotificationClient (Module 2); faked in specs to simulate the OS
// switching devices.

namespace Application.Ports;

public interface IDefaultDeviceWatcher : IDisposable
{
	/// <summary>Raised when the OS default capture device changes, carrying the new default's id.</summary>
	event EventHandler<DefaultDeviceChangedEventArgs>? DefaultChanged;

	/// <summary>Begins listening for default-device changes.</summary>
	void Start();

	/// <summary>Stops listening.</summary>
	void Stop();
}
