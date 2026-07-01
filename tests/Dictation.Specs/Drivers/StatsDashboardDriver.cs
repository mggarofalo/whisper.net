// Drives the stats dashboard scenarios. It owns HOW the dashboard is exercised so the steps
// stay one-liners: it builds the REAL StatsViewModel over the REAL Mediator pipeline (GetUsageStats
// handler + the REAL Logic usage-stats calculator) and a faked IHistoryStore. Because the aggregation is
// the real calculator's, the assertions prove the dashboard surfaces computed totals (it does no math of
// its own), reflects new activity on refresh, and shows zeroes for an empty history. The thin WPF view
// that binds to the ViewModel is Presentation glue verified by smoke.

using Application.History;
using Application.Ports;
using AwesomeAssertions;
using CommunityToolkit.Mvvm.Messaging;
using Domain.History;
using Logic.AppManagement.Shell;
using Mediator;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class StatsDashboardDriver
{
	private readonly StatsViewModel _viewModel;
	private readonly IHistoryStore _store;
	private readonly IMessenger _messenger;

	private readonly DateTimeOffset _day = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

	public StatsDashboardDriver(IMediator mediator, IMessenger messenger, IHistoryStore store)
	{
		_store = store;
		_messenger = messenger;
		_viewModel = new StatsViewModel(mediator, messenger, new Logic.AppManagement.Threading.InlineUiDispatcher());
	}

	// Two sessions totalling five words: "one two three" (3) + "four five" (2).
	public void StoreHasRecordedUsage() => ReturnEntries(
		new TranscriptEntry(Guid.NewGuid(), "one two three", _day),
		new TranscriptEntry(Guid.NewGuid(), "four five", _day.AddMinutes(5)));

	// Adds a third session of three more words ("six seven eight"), for 3 sessions / 8 words after refresh.
	public void MoreUsageIsRecorded() => ReturnEntries(
		new TranscriptEntry(Guid.NewGuid(), "one two three", _day),
		new TranscriptEntry(Guid.NewGuid(), "four five", _day.AddMinutes(5)),
		new TranscriptEntry(Guid.NewGuid(), "six seven eight", _day.AddMinutes(10)));

	public void StoreIsEmpty() => ReturnEntries();

	public Task OpenDashboard() => _viewModel.RefreshCommand.ExecuteAsync(null);

	public Task RefreshDashboard() => _viewModel.RefreshCommand.ExecuteAsync(null);

	// Grow the recorded usage and fire the live "transcription recorded" message exactly as the record path
	// does, WITHOUT calling Refresh, so the test proves the dashboard re-queries on the event itself.
	public async Task MoreUsageRecordedLive()
	{
		MoreUsageIsRecorded();
		_messenger.Send(new TranscriptionRecordedMessage(
			new TranscriptEntryDto(Guid.NewGuid(), "six seven eight", _day.AddMinutes(10), WordCount: 3)));
		await (_viewModel.RefreshCommand.ExecutionTask ?? Task.CompletedTask);
	}

	public void AssertTotals(int transcriptions, int words)
	{
		_viewModel.TotalTranscriptions.Should().Be(transcriptions);
		_viewModel.TotalWords.Should().Be(words);
		_viewModel.EstimatedTimeSaved.Should().BeGreaterThan(TimeSpan.Zero, "recorded words imply some time saved");
	}

	public void AssertZeroed()
	{
		_viewModel.TotalTranscriptions.Should().Be(0);
		_viewModel.TotalWords.Should().Be(0);
		_viewModel.EstimatedTimeSaved.Should().Be(TimeSpan.Zero);
	}

	private void ReturnEntries(params TranscriptEntry[] entries) =>
		_store.GetEntriesAsync(Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
			.Returns(entries);
}
