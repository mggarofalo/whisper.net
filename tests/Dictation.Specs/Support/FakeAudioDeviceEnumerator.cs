// A configurable stand-in for the capture-device enumerator, so the scenarios can drive
// device-selection policy without real hardware. The real NAudio enumerator is verified by manual
// smoke instead.

using Application.Ports;
using Domain.Audio;

namespace Dictation.Specs.Support;

public sealed class FakeAudioDeviceEnumerator : IAudioDeviceEnumerator
{
	private IReadOnlyList<AudioDevice> _devices = [];
	private string? _defaultId;

	public void Configure(IReadOnlyList<AudioDevice> devices, string? defaultId)
	{
		_devices = devices;
		_defaultId = defaultId;
	}

	public void SetDefault(string? defaultId) => _defaultId = defaultId;

	public IReadOnlyList<AudioDevice> GetCaptureDevices() => _devices;

	public string? GetSystemDefaultId() => _defaultId;
}
