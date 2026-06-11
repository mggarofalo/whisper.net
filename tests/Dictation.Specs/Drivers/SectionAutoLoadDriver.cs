// Drives the @WHISPER-108 section auto-load scenarios. It owns HOW the sections are exercised so the
// steps stay one-liners: every scenario enters through the REAL navigation lifecycle — the resolved
// ShellViewModel's NavigateCommand over the real NavigationService and the cached, scope-resolved
// feature view-models — never LoadCommand directly, because the activation-triggered load is exactly
// what this issue adds. The injected view-models are the same scoped instances navigation resolves, so
// the assertions observe precisely what the views would bind. Query counting happens at the substituted
// IHistoryStore seam (one BrowseHistory/GetUsageStats query = one GetEntriesAsync call), which is how
// "no duplicate queries" stays observable without instrumenting the Mediator pipeline.

using Application.Ports;
using AwesomeAssertions;
using Dictation.Specs.Support;
using Domain.Audio;
using Domain.History;
using Logic.AppManagement.Shell;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class SectionAutoLoadDriver
{
	private readonly ShellViewModel _shell;
	private readonly ModelViewModel _model;
	private readonly AudioDeviceViewModel _audio;
	private readonly HistoryViewModel _history;
	private readonly StatsViewModel _stats;
	private readonly IHistoryStore _historyStore;
	private readonly FakeAudioDeviceEnumerator _enumerator;

	private readonly DateTimeOffset _day = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

	// Completes the hanging history read in the double-fire scenario; the load is in flight until set.
	private readonly TaskCompletionSource<IReadOnlyList<TranscriptEntry>> _pendingLoad = new();

	private bool? _duplicateAttemptRefused;

	public SectionAutoLoadDriver(
		ShellViewModel shell,
		ModelViewModel model,
		AudioDeviceViewModel audio,
		HistoryViewModel history,
		StatsViewModel stats,
		IHistoryStore historyStore,
		FakeAudioDeviceEnumerator enumerator)
	{
		_shell = shell;
		_model = model;
		_audio = audio;
		_history = history;
		_stats = stats;
		_historyStore = historyStore;
		_enumerator = enumerator;

		// Resolving the shell already opened it on the Home dashboard (WHISPER-106), which reads history for
		// its overview. Those incidental calls are not what this issue tests, so clear the recorded calls
		// (keeping the configured return) — the counts below then reflect only the section under test.
		_historyStore.ClearReceivedCalls();
	}

	// --- given ---

	public void DevicesAvailable(string first, string second) =>
		_enumerator.Configure([new AudioDevice(first, first), new AudioDevice(second, second)], first);

	public void HistoryHolds(string text) =>
		ReturnEntries(new TranscriptEntry(Guid.NewGuid(), text, _day));

	// Two sessions totalling five words: "one two three" (3) + "four five" (2).
	public void HistoryHoldsTwoTranscriptions() => ReturnEntries(
		new TranscriptEntry(Guid.NewGuid(), "one two three", _day),
		new TranscriptEntry(Guid.NewGuid(), "four five", _day.AddMinutes(5)));

	public void HistoryLoadHangsUntilReleased() =>
		_historyStore.GetEntriesAsync(Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
			.Returns(_ => new ValueTask<IReadOnlyList<TranscriptEntry>>(_pendingLoad.Task));

	// --- when ---

	// Open through the REAL navigation lifecycle — NavigateCommand, exactly as a nav button does — then
	// settle the activation-triggered load so the Then steps observe the loaded state.
	public async Task OpenSection(string section)
	{
		_shell.NavigateCommand.Execute(section);
		await SettleLoad(section);
	}

	public async Task SwitchAwayAndBackTwice(string section)
	{
		for (int i = 0; i < 2; i++)
		{
			_shell.NavigateCommand.Execute("Home");
			_shell.NavigateCommand.Execute(section);
		}

		await SettleLoad(section);
	}

	// Refresh is the explicit manual re-query the auto-load must not replace.
	public Task RefreshHistory() => _history.LoadCommand.ExecuteAsync(null);

	// Navigate while the store read hangs: the activation-triggered load starts but cannot complete.
	public void OpenSectionWhileLoadPending(string section) => _shell.NavigateCommand.Execute(section);

	// The duplicate attempt honors the ICommand contract exactly as a bound control does: invoke only
	// when CanExecute allows it. AsyncRelayCommand disallows concurrent executions by default, so the
	// gate the view binds must refuse while the first load is still in flight.
	public void AttemptDuplicateLoadLikeTheView()
	{
		_duplicateAttemptRefused = !_history.LoadCommand.CanExecute(null);
		if (_duplicateAttemptRefused is false)
		{
			_history.LoadCommand.Execute(null);
		}
	}

	public async Task ReleasePendingLoad()
	{
		_pendingLoad.SetResult([new TranscriptEntry(Guid.NewGuid(), "released", _day)]);
		await (_history.LoadCommand.ExecutionTask ?? Task.CompletedTask);
	}

	// --- then ---

	public void AssertModelListPopulated() =>
		_model.Models.Should().NotBeEmpty("opening the Model section must populate the catalog with no manual refresh");

	public void AssertDevicesListed(string first, string second)
	{
		_audio.Devices.Select(device => device.Id).Should().Contain([first, second],
			"opening the Audio section must list the capture devices with no manual refresh");
		_audio.SelectedDeviceId.Should().NotBeNull("the persisted selection is part of the loaded state");
	}

	public void AssertHistoryShows(string text)
	{
		_history.Entries.Select(entry => entry.Text).Should().Contain(text,
			"opening the History section must load the first page with no manual refresh");
		_history.IsEmpty.Should().BeFalse();
	}

	// The totals only the real Application aggregation could produce: 2 sessions, 5 words.
	public void AssertRecordedTotalsShown()
	{
		_stats.TotalTranscriptions.Should().Be(2, "opening the Stats section must surface the totals with no manual refresh");
		_stats.TotalWords.Should().Be(5);
	}

	public void AssertHistoryQueriedExactly(int times) =>
		_historyStore.Received(times).GetEntriesAsync(Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>());

	public void AssertDuplicateAttemptRefused() =>
		_duplicateAttemptRefused.Should().BeTrue(
			"an in-flight load must report it cannot execute again, so no duplicate query can be fired");

	// --- helpers ---

	private async Task SettleLoad(string section)
	{
		Task? load = section switch
		{
			"Model" => _model.LoadCommand.ExecutionTask,
			"Audio" => _audio.LoadCommand.ExecutionTask,
			"History" => _history.LoadCommand.ExecutionTask,
			"Stats" => _stats.RefreshCommand.ExecutionTask,
			_ => null,
		};

		await (load ?? Task.CompletedTask);
	}

	private void ReturnEntries(params TranscriptEntry[] entries) =>
		_historyStore.GetEntriesAsync(Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
			.Returns(entries);
}
