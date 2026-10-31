// Drives the history browser scenarios. It owns HOW the browser is exercised so the steps
// stay one-liners: it builds the REAL HistoryViewModel over the REAL Mediator pipeline (SearchHistory +
// CopyToClipboard handlers) and faked IHistoryStore + IClipboard. The store substitute answers searches
// from the seeded entries (see HistoryStoreSearchContract), so the driver can prove the list is
// newest-first, that previous/next page through it, that a search filters in the store and pages over the
// matches, that a copy is dispatched, and that the empty and no-match states show — without a database or
// a real clipboard. The thin WPF view that binds to the ViewModel is Presentation glue verified by smoke.

using Application.History;
using Application.Ports;
using AwesomeAssertions;
using CommunityToolkit.Mvvm.Messaging;
using Domain.History;
using Logic.AppManagement.Shell;
using Mediator;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class HistoryBrowserDriver
{
	// One page is 25 entries (HistoryViewModel.PageSize); 35 spans two pages (25 + 10).
	private const int PageSize = 25;

	private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

	private readonly HistoryViewModel _viewModel;
	private readonly IHistoryStore _store;
	private readonly IClipboard _clipboard;

	private string? _copiedText;
	private string? _lastSearch;

	public HistoryBrowserDriver(IMediator mediator, IMessenger messenger, IHistoryStore store, IClipboard clipboard, IUiCollectionSynchronizer synchronizer)
	{
		_store = store;
		_clipboard = clipboard;
		_viewModel = new HistoryViewModel(mediator, messenger, synchronizer);
	}

	// --- given ---

	// Three entries returned out of chronological order, so the assertion proves the
	// view-model surfaces them newest-first rather than relying on store order.
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

	// 35 entries with increasing timestamps: a full first page of 25 and a short second page of 10.
	public void StoreHasEntriesAcrossTwoPages() => ReturnEntries(Numbered(PageSize + 10));

	// Exactly PageSize entries: the first page fills completely, and the total count alone must tell the
	// browser there is no further page.
	public void StoreHasExactlyOneFullPage() => ReturnEntries(Numbered(PageSize));

	public void StoreIsEmpty() => ReturnEntries([]);

	// The given texts, recorded a minute apart in the given (oldest-first) order.
	public void StoreHasEntries(IEnumerable<string> textsOldestFirst) =>
		ReturnEntries(textsOldestFirst
			.Select((text, i) => new TranscriptEntry(Guid.NewGuid(), text, Start.AddMinutes(i)))
			.ToArray());

	// `count` entries mention the term, interleaved with as many that do not.
	public void StoreHasEntriesMentioning(int count, string term) =>
		ReturnEntries(Enumerable.Range(0, count)
			.SelectMany(i => new TranscriptEntry[]
			{
				new(Guid.NewGuid(), $"{term} note {i:D2}", Start.AddMinutes(2 * i)),
				new(Guid.NewGuid(), $"grocery list {i:D2}", Start.AddMinutes((2 * i) + 1)),
			})
			.ToArray());

	// --- when ---

	public Task OpenHistory() => _viewModel.LoadCommand.ExecuteAsync(null);

	public Task BrowseToNextPage() => _viewModel.NextPageCommand.ExecuteAsync(null);

	public Task BrowseToPreviousPage() => _viewModel.PreviousPageCommand.ExecuteAsync(null);

	// Typing in the search box sets SearchText; the view-model re-runs the query from page 1 on its own.
	// Settle that query so the Then steps see its result.
	public async Task Search(string text)
	{
		_lastSearch = text;
		_viewModel.SearchText = text;
		await (_viewModel.LoadCommand.ExecutionTask ?? Task.CompletedTask);
	}

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
		// The second page replaces the first: the 10 oldest entries, still newest-first.
		AssertOnPage(2, 2);
		_viewModel.Entries.Should().HaveCount(10);
		_viewModel.Entries[0].Text.Should().Be("entry-09");
		_viewModel.Entries[^1].Text.Should().Be("entry-00");
	}

	public void AssertOnPage(int page, int of)
	{
		_viewModel.Page.Should().Be(page);
		_viewModel.TotalPages.Should().Be(of);
	}

	public void AssertNewestListedFirst() => _viewModel.Entries[0].Text.Should().Be($"entry-{PageSize + 9:D2}");

	public void AssertListed(IEnumerable<string> texts) =>
		_viewModel.Entries.Select(entry => entry.Text).Should().Equal(texts);

	public void AssertListedCount(int count) => _viewModel.Entries.Should().HaveCount(count);

	// The filter must run in the store, not over a full history pulled into the view-model: the search text
	// and the first page's bounds reach the port.
	public void AssertSearchAnsweredByStore() =>
		_store.Received().SearchAsync(_lastSearch, 0, PageSize, Arg.Any<CancellationToken>());

	public void AssertCopyDispatched() =>
		_clipboard.Received(1).SetText(_copiedText!);

	public void AssertEmptyState()
	{
		_viewModel.IsEmpty.Should().BeTrue();
		_viewModel.HasNoMatches.Should().BeFalse();
		_viewModel.Entries.Should().BeEmpty();
	}

	public void AssertNoMatchesState()
	{
		_viewModel.HasNoMatches.Should().BeTrue();
		_viewModel.Entries.Should().BeEmpty();
	}

	public void AssertNotEmptyHistoryState() => _viewModel.IsEmpty.Should().BeFalse();

	// The write-through assertion: the recorded transcription is read back through the
	// real Mediator pipeline into the view-model the History section binds — not off the store fake.
	public void AssertMostRecentEntryIs(string text)
	{
		_viewModel.Entries.Should().NotBeEmpty("a delivered transcription must be recorded in history");
		_viewModel.Entries[0].Text.Should().Be(text);
	}

	public void AssertFurtherPageOffered() => _viewModel.HasNextPage.Should().BeTrue();

	public void AssertNoFurtherPageOffered() => _viewModel.HasNextPage.Should().BeFalse();

	public void AssertNoPreviousPageOffered() => _viewModel.HasPreviousPage.Should().BeFalse();

	private static TranscriptEntry[] Numbered(int count) =>
		Enumerable.Range(0, count)
			.Select(i => new TranscriptEntry(Guid.NewGuid(), $"entry-{i:D2}", Start.AddMinutes(i)))
			.ToArray();

	private void ReturnEntries(IReadOnlyList<TranscriptEntry> entries) =>
		_store.GetEntriesAsync(Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
			.Returns(entries);
}
