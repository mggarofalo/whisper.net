// Inner TDD loop for the audio-device picker, WPF-free. These pin the device-list behavior
// behind IMediator: the list is loaded and the persisted selection reflected; a persisted device that is no
// longer present (by id or friendly name) does not crash or blank the picker but falls back to the system
// default and surfaces a clear warning (leaving the persisted id intact so it is restored if the device
// returns); a device whose id changed but whose name is still present is recovered and the stored id healed;
// and choosing a device commits it (live-applying via the settings pipeline) and clears the warning. The
// ComboBox view is Presentation glue verified by smoke.

using Application.Audio;
using Application.Settings;
using AwesomeAssertions;
using Domain.Audio;
using Logic.AppManagement.Shell;
using Logic.AudioManagement;
using Mediator;
using NSubstitute;
using Xunit;

namespace Logic.AppManagement.Tests.Shell;

public sealed class AudioDeviceViewModelTests
{
	private static readonly IReadOnlyList<AudioDeviceDto> Devices =
	[
		new("mic-a", "Microphone A", IsSystemDefault: true),
		new("mic-b", "Microphone B", IsSystemDefault: false),
	];

	private static AppSettingsDto SettingsWithDevice(string deviceId, string? deviceName = null) => new(
		ModelId: "base.en",
		Hotkey: "Ctrl+Shift+D",
		SilenceThresholdMs: 700,
		FillerWordRemovalEnabled: false,
		CaptureDeviceId: deviceId,
		CaptureDeviceName: deviceName);

	private static AudioDeviceViewModel ViewModelFor(string persistedDeviceId, string? persistedDeviceName = null)
	{
		IMediator mediator = Substitute.For<IMediator>();
		mediator.Send(Arg.Any<ListCaptureDevicesQuery>(), Arg.Any<CancellationToken>()).Returns(Devices);
		mediator.Send(Arg.Any<GetSettingsQuery>(), Arg.Any<CancellationToken>())
			.Returns(SettingsWithDevice(persistedDeviceId, persistedDeviceName));
		return new AudioDeviceViewModel(mediator, new DeviceSelectionPolicy());
	}

	[Fact]
	public async Task Loads_the_devices_and_reflects_a_present_persisted_selection()
	{
		AudioDeviceViewModel viewModel = ViewModelFor("mic-b");

		await viewModel.LoadCommand.ExecuteAsync(null);

		viewModel.Devices.Select(device => device.Name).Should().Contain(["Microphone A", "Microphone B"]);
		viewModel.SelectedDeviceId.Should().Be("mic-b");
		viewModel.UnavailableDeviceWarning.Should().BeNull();
	}

	[Fact]
	public async Task A_missing_persisted_device_falls_back_to_system_default_with_a_warning()
	{
		AudioDeviceViewModel viewModel = ViewModelFor("ghost-mic");

		await viewModel.LoadCommand.ExecuteAsync(null);

		viewModel.SelectedDeviceId.Should().Be(AudioDevice.SystemDefault, "a removed device falls back to the system default");
		viewModel.UnavailableDeviceWarning.Should().NotBeNullOrEmpty("the user is told the saved device is gone");
		viewModel.CommittedDeviceId.Should().Be("ghost-mic", "the persisted id is kept so the device is restored if it returns");
	}

	[Fact]
	public async Task A_changed_device_id_is_recovered_by_name_and_the_stored_id_is_healed()
	{
		// The endpoint id changed across a reboot, but the same friendly name ("Microphone B") is present.
		IMediator mediator = Substitute.For<IMediator>();
		mediator.Send(Arg.Any<ListCaptureDevicesQuery>(), Arg.Any<CancellationToken>()).Returns(Devices);
		mediator.Send(Arg.Any<GetSettingsQuery>(), Arg.Any<CancellationToken>())
			.Returns(SettingsWithDevice("mic-b-old-id", "Microphone B"));
		AudioDeviceViewModel viewModel = new(mediator, new DeviceSelectionPolicy());

		await viewModel.LoadCommand.ExecuteAsync(null);

		viewModel.SelectedDeviceId.Should().Be("mic-b", "the device is recovered under its current id by name");
		viewModel.UnavailableDeviceWarning.Should().BeNull("a recovered device is not a problem to warn about");
		await mediator.Received().Send(
			Arg.Is<UpdateSettingsCommand>(command =>
				command.Settings.CaptureDeviceId == "mic-b" && command.Settings.CaptureDeviceName == "Microphone B"),
			Arg.Any<CancellationToken>());
		viewModel.CommittedDeviceId.Should().Be("mic-b", "the stored id is healed so the warning never returns");
	}

	[Fact]
	public async Task Selecting_a_device_commits_it_with_its_name_and_clears_the_warning()
	{
		IMediator mediator = Substitute.For<IMediator>();
		mediator.Send(Arg.Any<ListCaptureDevicesQuery>(), Arg.Any<CancellationToken>()).Returns(Devices);
		mediator.Send(Arg.Any<GetSettingsQuery>(), Arg.Any<CancellationToken>()).Returns(SettingsWithDevice("ghost-mic"));
		AudioDeviceViewModel viewModel = new(mediator, new DeviceSelectionPolicy());
		await viewModel.LoadCommand.ExecuteAsync(null);

		await viewModel.SelectCommand.ExecuteAsync("mic-b");

		await mediator.Received().Send(
			Arg.Is<UpdateSettingsCommand>(command =>
				command.Settings.CaptureDeviceId == "mic-b" && command.Settings.CaptureDeviceName == "Microphone B"),
			Arg.Any<CancellationToken>());
		viewModel.SelectedDeviceId.Should().Be("mic-b");
		viewModel.CommittedDeviceId.Should().Be("mic-b");
		viewModel.UnavailableDeviceWarning.Should().BeNull();
	}
}
