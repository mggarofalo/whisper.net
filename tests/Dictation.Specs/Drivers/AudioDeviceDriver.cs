// Drives the device scenarios against the REAL DeviceSelectionPolicy and the REAL
// SettingsMapper (for the persistence round-trip), over a fake enumerator and a fake notification
// client. Device ids equal their display names here for readability. When the fake watcher reports a
// default change, the driver updates the enumerator's default — mirroring how the app reacts to the
// OS switching devices.

using AwesomeAssertions;
using Dictation.Specs.Support;
using Domain.Audio;
using Domain.Settings;
using Logic.AudioManagement;

namespace Dictation.Specs.Drivers;

public sealed class AudioDeviceDriver
{
	private readonly DeviceSelectionPolicy _policy;
	private readonly Application.Settings.SettingsMapper _mapper;
	private readonly FakeAudioDeviceEnumerator _enumerator;
	private readonly FakeDefaultDeviceWatcher _watcher;

	private string _selected = AudioDevice.SystemDefault;
	private string? _persisted;
	private DeviceResolution? _resolution;

	public AudioDeviceDriver(
		DeviceSelectionPolicy policy,
		Application.Settings.SettingsMapper mapper,
		FakeAudioDeviceEnumerator enumerator,
		FakeDefaultDeviceWatcher watcher)
	{
		_policy = policy;
		_mapper = mapper;
		_enumerator = enumerator;
		_watcher = watcher;

		// React to the OS default changing exactly as the app would: re-point at the new default.
		_watcher.DefaultChanged += (_, e) => _enumerator.SetDefault(e.NewDefaultId);
	}

	public void DefaultChangesTo(string id) => _watcher.RaiseDefaultChanged(id);

	public void DevicesAvailable(string first, string second, string defaultName) =>
		_enumerator.Configure([new AudioDevice(first, first), new AudioDevice(second, second)], defaultName);

	public void SelectDevice(string id) => Persist(id);

	public void FollowSystemDefault() => Persist(AudioDevice.SystemDefault);

	public void SelectMissingDevice() => Persist("removed-device");

	// Persist the selection through the real settings mapper (domain <-> DTO round-trip) and read it
	// back — the same projection that carries the choice to and from configuration.
	private void Persist(string deviceId)
	{
		Application.Settings.AppSettingsDto dto = new(
			AppSettings.Default.ModelId,
			AppSettings.Default.Hotkey.Chord,
			AppSettings.Default.SilenceThresholdMs,
			AppSettings.Default.FillerWordRemovalEnabled,
			CaptureDeviceId: deviceId);

		AppSettings saved = _mapper.ToDomain(dto);
		_persisted = _mapper.ToDto(saved).CaptureDeviceId;
		_selected = _persisted;
	}

	public void Restart() => _selected = _persisted ?? AudioDevice.SystemDefault;

	public void Resolve() => _resolution =
		_policy.Resolve(_selected, _enumerator.GetCaptureDevices(), _enumerator.GetSystemDefaultId());

	public void AssertUsesDevice(string id)
	{
		Resolve();
		_resolution!.DeviceId.Should().Be(id);
	}

	public void AssertSubstitutionReported() => _resolution!.Substituted.Should().BeTrue();
}
