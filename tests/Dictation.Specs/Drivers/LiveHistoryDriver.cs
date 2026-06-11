// Drives the @WHISPER-114 live-history scenarios. It builds the REAL HistoryViewModel over the REAL
// Mediator pipeline (BrowseHistory to load, RecordTranscription to record) and the scenario's shared
// messenger + collection synchronizer, faking only the history store. The section is entered through the
// REAL activation lifecycle (OnNavigatedTo registers the live feed and runs the first-activation load),
// so recording a transcription while it is open publishes the WHISPER-114 message that prepends the new
// entry live — no Refresh, no re-query, preserving the loaded page.

using Application.History;
using Application.Ports;
using AwesomeAssertions;
using CommunityToolkit.Mvvm.Messaging;
using Domain.History;
using Logic.AppManagement.Shell;
using Mediator;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class LiveHistoryDriver
{
	private static readonly DateTimeOffset Base = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

	private readonly HistoryViewModel _viewModel;
	private readonly IMediator _mediator;
	private readonly IHistoryStore _store;

	private int _recorded;
	private string? _lastRecorded;

	public LiveHistoryDriver(IMediator mediator, IMessenger messenger, IHistoryStore store, IUiCollectionSynchronizer synchronizer)
	{
		_mediator = mediator;
		_store = store;
		_viewModel = new HistoryViewModel(mediator, messenger, synchronizer);
	}

	public void HistoryAlreadyHas(params string[] texts) =>
		_store.GetEntriesAsync(Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
			.Returns(texts.Select((text, index) => new TranscriptEntry(Guid.NewGuid(), text, Base.AddMinutes(index))).ToArray());

	// Open through the real lifecycle: OnNavigatedTo registers the live feed (OnActivated) and runs the
	// first-activation load; settle that load so the Then steps see the loaded state.
	public async Task OpenHistory()
	{
		_viewModel.OnNavigatedTo();
		await (_viewModel.LoadCommand.ExecutionTask ?? Task.CompletedTask);
	}

	// Switch away (deactivate -> unregister the feed) then back (re-activate; the cached section does not
	// re-query, WHISPER-108), so a live entry added while active must still be present.
	public async Task SwitchAwayAndBack()
	{
		_viewModel.OnNavigatedFrom();
		_viewModel.OnNavigatedTo();
		await (_viewModel.LoadCommand.ExecutionTask ?? Task.CompletedTask);
	}

	// Record through the real Mediator pipeline; the handler persists and publishes the live message.
	public Task RecordDictation(string text)
	{
		_lastRecorded = text;
		return _mediator.Send(new RecordTranscriptionCommand(text, Base.AddHours(1).AddMinutes(_recorded++))).AsTask();
	}

	public void AssertTopIsLastRecorded()
	{
		_viewModel.Entries.Should().NotBeEmpty();
		_viewModel.Entries[0].Text.Should().Be(_lastRecorded);
	}

	public void AssertEntryCount(int count) => _viewModel.Entries.Should().HaveCount(count);

	public void AssertNotEmpty() => _viewModel.IsEmpty.Should().BeFalse();

	public void AssertListContains(string text) =>
		_viewModel.Entries.Select(entry => entry.Text).Should().Contain(text);
}
