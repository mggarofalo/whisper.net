// Drives the Home status-dashboard scenarios. It builds the REAL HomeViewModel over the
// REAL Mediator pipeline (GetSettings / ListCaptureDevices / GetUsageStats + the real Logic calculator /
// BrowseHistory handlers), faking only the device-facing ports (settings store, history store, device
// enumerator). So it proves the dashboard surfaces genuinely live status from settings and history — the
// active model, the input device's friendly name, the hotkey, the computed usage totals, and the recent
// transcriptions — entered through the REAL activation lifecycle (OnNavigatedTo triggers the first-
// activation load). The thin WPF view that binds to it is Presentation glue verified by smoke.

using Application.Ports;
using AwesomeAssertions;
using CommunityToolkit.Mvvm.Messaging;
using Dictation.Specs.Support;
using Domain.Audio;
using Domain.History;
using Domain.Settings;
using Logic.AppManagement.Shell;
using Mediator;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class HomeDashboardDriver
{
	private const string DeviceId = "mic-1";

	private readonly HomeViewModel _viewModel;
	private readonly ISettingsStore _settings;
	private readonly IHistoryStore _history;
	private readonly FakeAudioDeviceEnumerator _devices;
	private readonly IMessenger _messenger;

	public HomeDashboardDriver(IMediator mediator, IMessenger messenger, ISettingsStore settings, IHistoryStore history, FakeAudioDeviceEnumerator devices, IUiCollectionSynchronizer synchronizer)
	{
		_settings = settings;
		_history = history;
		_devices = devices;
		_messenger = messenger;
		_viewModel = new HomeViewModel(mediator, messenger, synchronizer, new Logic.AppManagement.Threading.InlineUiDispatcher());
	}

	public void GivenSettings(string modelId, string deviceName)
	{
		_settings.LoadAsync(Arg.Any<CancellationToken>()).Returns(SettingsWith(modelId, DeviceId));
		_devices.Configure([new AudioDevice(DeviceId, deviceName)], defaultId: DeviceId);
	}

	public void GivenSettingsWithSystemDefaultDevice(string modelId) =>
		_settings.LoadAsync(Arg.Any<CancellationToken>()).Returns(SettingsWith(modelId, AudioDevice.SystemDefault));

	// AppSettings exposes get-only properties (no init setters), so a `with` clause cannot retarget them;
	// rebuild via the constructor, keeping every other field at the default the store would return.
	private static AppSettings SettingsWith(string modelId, string captureDeviceId)
	{
		AppSettings d = AppSettings.Default;
		return new AppSettings(modelId, d.Hotkey, d.SilenceThresholdMs, d.FillerWordRemovalEnabled, captureDeviceId, d.AuditLogEnabled, d.SetupCompleted);
	}

	public void GivenRecordedUsage() => ReturnEntries(
		new TranscriptEntry(Guid.NewGuid(), "one two three", new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero)),
		new TranscriptEntry(Guid.NewGuid(), "four five", new DateTimeOffset(2026, 1, 1, 9, 5, 0, TimeSpan.Zero)));

	public void GivenNoHistory() => ReturnEntries();

	// Enter the section through the real activation lifecycle so the activation refresh runs, then await it.
	public async Task OpenDashboard()
	{
		_viewModel.OnNavigatedTo();
		await _viewModel.RefreshCommand.ExecutionTask!;
	}

	// Simulate activity since Home was last open, then switch away and back; the dashboard
	// must re-query on activation rather than show a stale snapshot.
	public void ANewTranscriptionIsRecorded(string text) =>
		ReturnEntries(new TranscriptEntry(Guid.NewGuid(), text, new DateTimeOffset(2026, 6, 11, 21, 0, 0, TimeSpan.Zero)));

	public async Task ReopenDashboard()
	{
		_viewModel.OnNavigatedFrom();
		_viewModel.OnNavigatedTo();
		await _viewModel.RefreshCommand.ExecutionTask!;
	}

	// Record activity and fire the live "transcription recorded" message exactly as the record path does,
	// WITHOUT reopening, so the test proves the dashboard re-queries on the event itself while it stays open.
	public async Task ATranscriptionIsRecordedLive(string text)
	{
		ANewTranscriptionIsRecorded(text);
		_messenger.Send(new Application.History.TranscriptionRecordedMessage(
			new Application.History.TranscriptEntryDto(
				Guid.NewGuid(), text, new DateTimeOffset(2026, 6, 11, 21, 0, 0, TimeSpan.Zero), WordCount: 2)));
		await (_viewModel.RefreshCommand.ExecutionTask ?? Task.CompletedTask);
	}

	public void AssertMostRecentIs(string text)
	{
		_viewModel.Recent.Should().NotBeEmpty();
		_viewModel.Recent[0].Text.Should().Be(text);
	}

	public void AssertActiveModel(string modelId) => _viewModel.ActiveModel.Should().Be(modelId);

	public void AssertInputDevice(string deviceName) => _viewModel.InputDevice.Should().Be(deviceName);

	public void AssertShowsAHotkey() => _viewModel.Hotkey.Should().NotBeNullOrWhiteSpace("the dashboard surfaces the configured hotkey");

	public void AssertTotals(int transcriptions, int words)
	{
		_viewModel.TotalTranscriptions.Should().Be(transcriptions);
		_viewModel.TotalWords.Should().Be(words);
	}

	public void AssertZeroTotals()
	{
		_viewModel.TotalTranscriptions.Should().Be(0);
		_viewModel.TotalWords.Should().Be(0);
	}

	public void AssertListsRecent(int count)
	{
		_viewModel.Recent.Should().HaveCount(count);
		_viewModel.IsEmpty.Should().BeFalse();
	}

	public void AssertEmptyRecent()
	{
		_viewModel.Recent.Should().BeEmpty();
		_viewModel.IsEmpty.Should().BeTrue();
	}

	private void ReturnEntries(params TranscriptEntry[] entries) =>
		_history.GetEntriesAsync(Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
			.Returns(entries);
}
