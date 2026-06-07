// The real WASAPI notification client: wraps NAudio's endpoint-notification callback so the app is
// told when the OS default capture device changes (the signal that drives hot-swap). The underlying
// enumerator is created on Start, so resolving the port touches no hardware. Device glue — verified
// by manual real-device smoke; the specs raise the DefaultChanged event from a fake instead.

using Application.Ports;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace Infrastructure.Audio;

internal sealed class NAudioDefaultDeviceWatcher : IDefaultDeviceWatcher, IMMNotificationClient
{
	private MMDeviceEnumerator? _enumerator;
	private bool _registered;

	public event EventHandler<DefaultDeviceChangedEventArgs>? DefaultChanged;

	public void Start()
	{
		_enumerator ??= new MMDeviceEnumerator();
		if (!_registered)
		{
			_enumerator.RegisterEndpointNotificationCallback(this);
			_registered = true;
		}
	}

	public void Stop()
	{
		if (_registered && _enumerator is not null)
		{
			_enumerator.UnregisterEndpointNotificationCallback(this);
			_registered = false;
		}
	}

	// IMMNotificationClient: only the default *capture* device change is relevant here.
	public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
	{
		if (flow == DataFlow.Capture)
		{
			DefaultChanged?.Invoke(this, new DefaultDeviceChangedEventArgs(defaultDeviceId));
		}
	}

	public void OnDeviceAdded(string pwstrDeviceId)
	{
	}

	public void OnDeviceRemoved(string deviceId)
	{
	}

	public void OnDeviceStateChanged(string deviceId, DeviceState newState)
	{
	}

	public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key)
	{
	}

	public void Dispose()
	{
		Stop();
		_enumerator?.Dispose();
		_enumerator = null;
	}
}
