// Drives the @WHISPER-45 history browser scenarios. It owns HOW the browser is exercised so the steps
// stay one-liners: it builds the REAL HistoryViewModel over the REAL Mediator pipeline (BrowseHistory +
// CopyToClipboard handlers, including the paging/ordering the handler owns) and faked IHistoryStore +
// IClipboard. It can therefore prove the list is newest-first, that browsing loads the next page, that a
// copy is dispatched, and that an empty history is an empty state — without a database or a real
// clipboard. The thin WPF view that binds to the ViewModel is Presentation glue verified by smoke.

using Application.History;
using Application.Ports;
using AwesomeAssertions;
using Domain.History;
using Logic.AppManagement.Shell;
using Mediator;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class HistoryBrowserDriver
{
	// One page is 50 entries (HistoryViewModel.PageSize); 60 spans two pages (50 + 10).
	private const int PageSize = 50;

	private readonly HistoryViewModel _viewModel;
	private readonly IHistoryStore _store;
	private readonly IClipboard _clipboard;

	private string? _copiedText;

	public HistoryBrowserDriver(IMediator mediator, IHistoryStore store, IClipboard clipboard, IUiCollectionSynchronizer synchronizer)
	{
		_store = store;
		_clipboard = clipboard;
		_viewModel = new HistoryViewModel(mediator, synchronizer);
	}

	// --- given ---

	// Three entries returned out of chronological order, so the assertion proves the handler/view-model
	// surface them newest-first rather than relying on store order.
	public void StoreHasThreeOutOfOrderEntries()
	{
		DateTimeOffset day = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
		TranscriptEntry[] entries =
		[
			new(Guid.NewGuid(), "oldest", day),
			new(Guid.NewGuid(), "newest", day.AddHours(2)),
			new(Guid.NewGuid(), "middle", day.AddHours(1)),
		];
		ReturnEntries(entries);
	}

	public void StoreHasEntriesAcrossTwoPages()
	{
		// 60 entries with increasing timestamps; the handler orders newest-first and pages by 50.
		DateTimeOffset start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
		TranscriptEntry[] entries = Enumerable.Range(0, 60)
			.Select(i => new TranscriptEntry(Guid.NewGuid(), $"entry-{i:D2}", start.AddMinutes(i)))
			.ToArray();
		ReturnEntries(entries);
	}

	public void StoreIsEmpty() => ReturnEntries([]);

	// --- when ---

	public Task OpenHistory() => _viewModel.LoadCommand.ExecuteAsync(null);

	public Task BrowseToNextPage() => _viewModel.NextPageCommand.ExecuteAsync(null);

	public Task CopyFirstEntry()
	{
		TranscriptEntryDto first = _viewModel.Entries[0];
		_copiedText = first.Text;
		return _viewModel.CopyCommand.ExecuteAsync(first);
	}

	// --- then ---

	public void AssertListedNewestFirst() =>
		_viewModel.Entries.Select(entry => entry.Text).Should().Equal("newest", "middle", "oldest");

	public void AssertNextPageShown()
	{
		// The second page (10 more) was appended to the first (50), newest-first across both.
		_viewModel.Entries.Should().HaveCount(60);
		_viewModel.Entries[0].Text.Should().Be("entry-59");
		_viewModel.Entries[^1].Text.Should().Be("entry-00");
	}

	public void AssertCopyDispatched() =>
		_clipboard.Received(1).SetText(_copiedText!);

	public void AssertEmptyState()
	{
		_viewModel.IsEmpty.Should().BeTrue();
		_viewModel.Entries.Should().BeEmpty();
	}

	private void ReturnEntries(IReadOnlyList<TranscriptEntry> entries) =>
		_store.GetEntriesAsync(Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
			.Returns(entries);
}
