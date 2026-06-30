// Drives the audio-device picker scenarios. It exercises the REAL AudioDeviceViewModel over the
// REAL Mediator pipeline (ListCaptureDevices / GetSettings / UpdateSettings) and the REAL settings mapper,
// faking only the device enumerator and the settings store (with a round-trip so a commit is visible to the
// next load). So it proves at the view-model boundary: the picker lists active devices by friendly name and
// a selection commits, and a persisted device that is no longer present falls back to the system default
// with a clear warning instead of crashing or blanking. The ComboBox view is Presentation glue (smoke).

using Application.Ports;
using AwesomeAssertions;
using Dictation.Specs.Support;
using Domain.Audio;
using Domain.Settings;
using Logic.AppManagement.Shell;
using Logic.AudioManagement;
using Mediator;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class AudioDevicePickerDriver
{
	private readonly AudioDeviceViewModel _viewModel;
	private readonly FakeAudioDeviceEnumerator _enumerator;
	private readonly ISettingsStore _store;

	private AppSettings _persisted =
		new("base.en", HotkeyBinding.Parse("Ctrl+Shift+D"), silenceThresholdMs: 700, fillerWordRemovalEnabled: false);

	public AudioDevicePickerDriver(IMediator mediator, FakeAudioDeviceEnumerator enumerator, ISettingsStore store)
	{
		_enumerator = enumerator;
		_store = store;
		_viewModel = new AudioDeviceViewModel(mediator, new DeviceSelectionPolicy());

		_store.LoadAsync(Arg.Any<CancellationToken>()).Returns(_ => _persisted);
		_store.When(s => s.SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>()))
			.Do(call => _persisted = call.Arg<AppSettings>());
	}

	// Two named capture devices, the first the OS default.
	public void TwoDevicesAvailable() =>
		_enumerator.Configure(
			[new AudioDevice("mic-a", "Microphone A"), new AudioDevice("mic-b", "Microphone B")],
			"mic-a");

	// Pre-persist a device id that is not in the enumerated list, modelling a removed/unplugged microphone.
	// AppSettings has get-only members, so the value is rebuilt through its constructor.
	public void SavedDeviceIsMissing(string deviceId) => _persisted = new AppSettings(
		_persisted.ModelId,
		_persisted.Hotkey,
		_persisted.SilenceThresholdMs,
		_persisted.FillerWordRemovalEnabled,
		deviceId,
		_persisted.AuditLogEnabled,
		_persisted.SetupCompleted);

	// Pre-persist a selection whose endpoint id has changed but whose friendly name still matches a present
	// device, modelling a USB/Bluetooth/dock mic re-enumerated under a new id across a reboot.
	public void SavedDeviceIdChangedButNameMatches(string staleId, string name) => _persisted = new AppSettings(
		_persisted.ModelId,
		_persisted.Hotkey,
		_persisted.SilenceThresholdMs,
		_persisted.FillerWordRemovalEnabled,
		staleId,
		_persisted.AuditLogEnabled,
		_persisted.SetupCompleted,
		captureDeviceName: name);

	public Task LoadDevices() => _viewModel.LoadCommand.ExecuteAsync(null);

	public Task PickDevice(string deviceId) => _viewModel.SelectCommand.ExecuteAsync(deviceId);

	// A selection change as the ComboBox's two-way SelectedValue binding performs it: set
	// the property and await the commit the view-model decides to make (if any).
	public async Task ChangeSelection(string deviceId)
	{
		_viewModel.SelectedDeviceId = deviceId;
		if (_viewModel.SelectCommand.ExecutionTask is { } commit)
		{
			await commit;
		}
	}

	public void AssertListedByName(params string[] names) =>
		_viewModel.Devices.Select(device => device.Name).Should().Contain(names);

	public void AssertCommitted(string deviceId) =>
		_store.Received().SaveAsync(
			Arg.Is<AppSettings>(settings => settings.CaptureDeviceId == deviceId),
			Arg.Any<CancellationToken>());

	public void AssertFellBackToSystemDefault() =>
		_viewModel.SelectedDeviceId.Should().Be(AudioDevice.SystemDefault);

	public void AssertCommittedExactlyOnce(string deviceId)
	{
		_store.Received(1).SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>());
		_persisted.CaptureDeviceId.Should().Be(deviceId);
	}

	public void AssertNothingCommitted() =>
		_store.DidNotReceive().SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>());

	public void AssertUnavailableWarningShown() =>
		_viewModel.UnavailableDeviceWarning.Should().NotBeNullOrEmpty();

	public void AssertSelectedDevice(string deviceId) =>
		_viewModel.SelectedDeviceId.Should().Be(deviceId);

	public void AssertNoUnavailableWarning() =>
		_viewModel.UnavailableDeviceWarning.Should().BeNullOrEmpty();

	// The stored id was healed to the device's current id, so the next launch matches by id with no warning.
	public void AssertHealedTo(string deviceId) =>
		_persisted.CaptureDeviceId.Should().Be(deviceId);
}
