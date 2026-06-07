// The real capture-device enumerator: wraps NAudio's MMDeviceEnumerator to list active capture
// devices and identify the OS default. The enumerator is created lazily so resolving the port never
// touches audio hardware — only an actual enumeration call does. Device glue, so it is verified by
// manual real-device smoke; the specs drive a fake enumerator instead.

using Application.Ports;
using Domain.Audio;
using NAudio.CoreAudioApi;

namespace Infrastructure.Audio;

internal sealed class NAudioDeviceEnumerator : IAudioDeviceEnumerator, IDisposable
{
	private MMDeviceEnumerator? _enumerator;

	private MMDeviceEnumerator Enumerator => _enumerator ??= new MMDeviceEnumerator();

	public IReadOnlyList<AudioDevice> GetCaptureDevices()
	{
		List<AudioDevice> devices = [];
		foreach (MMDevice device in Enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
		{
			devices.Add(new AudioDevice(device.ID, device.FriendlyName));
		}

		return devices;
	}

	public string? GetSystemDefaultId()
	{
		if (!Enumerator.HasDefaultAudioEndpoint(DataFlow.Capture, Role.Communications))
		{
			return null;
		}

		using MMDevice defaultDevice = Enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
		return defaultDevice.ID;
	}

	public void Dispose()
	{
		_enumerator?.Dispose();
		_enumerator = null;
	}
}
