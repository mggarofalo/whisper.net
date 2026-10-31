// The shell's history browser: lists past transcriptions newest-first one page at a time, with
// previous/next paging, a page indicator, and a text search, and re-copies an entry to the clipboard. It
// reads via SearchHistoryQuery, whose filter and paging run in the store, so a large history is never loaded
// whole; it copies via CopyToClipboardCommand. Changing the search text re-queries from page 1. An empty
// history and a search with no matches are distinct first-class states, not errors. Built on
// CommunityToolkit.Mvvm and WPF-free so the behavior is driven for real in specs; the thin view binds to it.
// Entries is a UiBoundCollection registered through the collection-sync seam at construction, so an
// off-UI-thread mutation (the live feed, published from the record path) binds safely instead of throwing.
// The live feed is subscribed for the section's whole lifetime (so an entry recorded while another section
// is open is not missed) and prepends a new entry to the first page without re-querying.

using Application.History;
using Application.Ports;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Mediator;

namespace Logic.AppManagement.Shell;

public sealed partial class HistoryViewModel : FeatureViewModel
{
	private const int PageSize = 25;

	private readonly IMediator _mediator;
	private readonly IMessenger _messenger;

	// Bumped by every page load; a load whose result arrives after a newer one started is discarded, so a
	// slow query for an earlier keystroke can never overwrite the results for the latest one.
	private int _loadVersion;

	public HistoryViewModel(IMediator mediator, IMessenger messenger, IUiCollectionSynchronizer synchronizer)
	{
		_mediator = mediator;
		_messenger = messenger;
		synchronizer.Enable(Entries);

		// Live history feed: subscribe for the section's whole lifetime, not just while it is the active
		// content, so a transcription recorded while another section is open still lands in the list and
		// reopening History shows it without a re-query (which would disturb the browsed page). The
		// shared WeakReferenceMessenger holds the recipient weakly, so a persistent registration neither
		// roots nor leaks this cached view-model.
		_messenger.Register<HistoryViewModel, TranscriptionRecordedMessage>(
			this, static (recipient, message) => recipient.OnTranscriptionRecorded(message.Entry));
	}

	/// <summary>The current page of history entries, newest first.</summary>
	public UiBoundCollection<TranscriptEntryDto> Entries { get; } = [];

	/// <summary>The search box text. Changing it re-queries from the first page.</summary>
	[ObservableProperty]
	private string _searchText = string.Empty;

	// Paging availability is exposed as plain properties the view binds IsEnabled to, not as command
	// CanExecute: the live feed updates the counts from a background thread, and a CanExecuteChanged raised
	// there would reach WPF buttons off the UI thread, while a property change binds safely.

	/// <summary>The 1-based index of the page shown.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasPreviousPage))]
	[NotifyPropertyChangedFor(nameof(HasNextPage))]
	private int _page = 1;

	/// <summary>How many pages the current search (or the whole history) spans; never below one.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasNextPage))]
	private int _totalPages = 1;

	/// <summary>Whether a newer page exists, so the view can disable Previous on the first page.</summary>
	public bool HasPreviousPage => Page > 1;

	/// <summary>Whether an older page exists, so the view can disable Next on the last page.</summary>
	public bool HasNextPage => Page < TotalPages;

	/// <summary>How many entries match the current search (or exist, with no search).</summary>
	[ObservableProperty]
	private int _totalCount;

	/// <summary>True when there is no history at all — the view renders an empty state, not an error.</summary>
	[ObservableProperty]
	private bool _isEmpty;

	/// <summary>True when a search is active and nothing matches it — distinct from an empty history.</summary>
	[ObservableProperty]
	private bool _hasNoMatches;

	// Auto-load the first page on first activation: the browser opens populated (or in its
	// first-class empty state), the cached instance does not re-query on later tab switches — so the page
	// the user browsed to survives a tab switch — and Refresh stays the manual re-query.
	protected override IAsyncRelayCommand FirstActivationLoadCommand => LoadCommand;

	// A new search starts over at page 1. Fire-and-forget through LoadCommand so the view's binding stays
	// a plain property set; LoadCommand cancels any query still running for the previous text.
	partial void OnSearchTextChanged(string value) => LoadCommand.Execute(null);

	// Load the first page for the current search through Mediator, replacing whatever was shown.
	[RelayCommand]
	private Task LoadAsync(CancellationToken cancellationToken) => LoadPageAsync(1, cancellationToken);

	// Browse to the older page; a no-op on the last page (the view disables the button there too).
	[RelayCommand]
	private Task NextPageAsync(CancellationToken cancellationToken) =>
		HasNextPage ? LoadPageAsync(Page + 1, cancellationToken) : Task.CompletedTask;

	// Browse to the newer page; a no-op on the first page.
	[RelayCommand]
	private Task PreviousPageAsync(CancellationToken cancellationToken) =>
		HasPreviousPage ? LoadPageAsync(Page - 1, cancellationToken) : Task.CompletedTask;

	private async Task LoadPageAsync(int page, CancellationToken cancellationToken)
	{
		int version = Interlocked.Increment(ref _loadVersion);
		string search = SearchText;

		HistoryPageDto result;
		try
		{
			result = await _mediator.Send(new SearchHistoryQuery(search, page, PageSize), cancellationToken);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			// Superseded by a newer load (LoadCommand cancels its previous run); that load owns the list.
			return;
		}

		if (version != Volatile.Read(ref _loadVersion))
		{
			return;
		}

		Entries.Clear();
		foreach (TranscriptEntryDto entry in result.Entries)
		{
			Entries.Add(entry);
		}

		TotalCount = result.TotalCount;
		TotalPages = result.TotalPages;
		Page = result.Page;

		bool searching = !string.IsNullOrWhiteSpace(search);
		IsEmpty = result.TotalCount == 0 && !searching;
		HasNoMatches = result.TotalCount == 0 && searching;
	}

	// A newly recorded entry is the newest of all, so it belongs at the top of page 1 — if it matches the
	// current search. Prepend it there without a re-query (the page stays put and keeps its size by dropping
	// its oldest row); on a later page only the counts move. The collection-sync seam makes this safe off the
	// UI thread (the record path publishes from a background thread). Dedupe by id so a redelivery never
	// doubles up.
	private void OnTranscriptionRecorded(TranscriptEntryDto entry)
	{
		string search = SearchText.Trim();
		if (search.Length > 0 && !entry.Text.Contains(search, StringComparison.OrdinalIgnoreCase))
		{
			return;
		}

		if (Entries.Any(existing => existing.Id == entry.Id))
		{
			return;
		}

		if (Page == 1)
		{
			Entries.Insert(0, entry);
			if (Entries.Count > PageSize)
			{
				Entries.RemoveAt(Entries.Count - 1);
			}
		}

		TotalCount++;
		TotalPages = (TotalCount + PageSize - 1) / PageSize;
		IsEmpty = false;
		HasNoMatches = false;
	}

	// Re-copy a past transcription's text to the clipboard via Mediator.
	[RelayCommand]
	private async Task CopyAsync(TranscriptEntryDto? entry, CancellationToken cancellationToken)
	{
		if (entry is null)
		{
			return;
		}

		await _mediator.Send(new CopyToClipboardCommand(entry.Text), cancellationToken);
	}
}
