// The shell's history browser: lists past transcriptions newest-first, pages through them,
// and re-copies an entry to the clipboard. It reads via BrowseHistoryQuery (a page at a time, so a
// large history never blocks) and copies via CopyToClipboardCommand. Empty history is a first-class
// empty state, not an error, and HasMorePages tells the view when Load More can no
// longer produce anything so the control disables instead of silently no-opping. Built on CommunityToolkit.Mvvm and WPF-free so the behavior is driven for
// real in specs; the thin view binds to it. Entries is a UiBoundCollection registered through the
// collection-sync seam at construction, so an off-UI-thread mutation (the live
// feed, published from the record path) binds safely instead of throwing. The live feed is subscribed
// only while the section is active (messenger discipline) and prepends the new entry without
// re-querying, so the user's browsed page and scroll position are preserved.

using Application.History;
using Application.Ports;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Mediator;

namespace Logic.AppManagement.Shell;

public sealed partial class HistoryViewModel : FeatureViewModel
{
	private const int PageSize = 50;

	private readonly IMediator _mediator;
	private readonly IMessenger _messenger;

	public HistoryViewModel(IMediator mediator, IMessenger messenger, IUiCollectionSynchronizer synchronizer)
	{
		_mediator = mediator;
		_messenger = messenger;
		synchronizer.Enable(Entries);
	}

	// Live history feed: subscribe to the "transcription recorded" message only while this
	// section is active, so an inactive cached instance holds no subscription. The shared
	// WeakReferenceMessenger means even a missed deactivation could not root this cached view-model.
	protected override void OnActivated() =>
		_messenger.Register<HistoryViewModel, TranscriptionRecordedMessage>(this, (recipient, message) => recipient.OnTranscriptionRecorded(message.Entry));

	protected override void OnDeactivated() => _messenger.UnregisterAll(this);

	// Prepend a newly recorded entry so it shows newest-first without a Refresh, leaving the already-loaded
	// pages and scroll position untouched. The collection-sync seam makes this safe off the UI thread (the
	// record path publishes from a background thread). Dedupe by id so a redelivery (or an entry already on
	// the loaded page) never doubles up.
	private void OnTranscriptionRecorded(TranscriptEntryDto entry)
	{
		if (Entries.Any(existing => existing.Id == entry.Id))
		{
			return;
		}

		Entries.Insert(0, entry);
		IsEmpty = false;
	}

	/// <summary>The loaded history entries, newest first, growing as further pages are browsed.</summary>
	public UiBoundCollection<TranscriptEntryDto> Entries { get; } = [];

	/// <summary>True when there is no history to show — the view renders an empty state, not an error.</summary>
	[ObservableProperty]
	private bool _isEmpty;

	/// <summary>The 1-based index of the most recent page loaded.</summary>
	[ObservableProperty]
	private int _page = 1;

	/// <summary>
	/// Whether a further history page may exist. True until a load proves otherwise, so the view can
	/// disable Load More instead of offering a silent no-op once the history is exhausted.
	/// </summary>
	[ObservableProperty]
	private bool _hasMorePages = true;

	// Auto-load the first page on first activation: the browser opens populated (or in its
	// first-class empty state), the cached instance does not re-query on later tab switches — so the page
	// the user browsed to survives a tab switch — and Refresh stays the manual re-query.
	protected override IAsyncRelayCommand FirstActivationLoadCommand => LoadCommand;

	// Load the first page through Mediator, replacing whatever was shown.
	[RelayCommand]
	private async Task LoadAsync(CancellationToken cancellationToken)
	{
		Page = 1;
		IReadOnlyList<TranscriptEntryDto> first = await _mediator.Send(new BrowseHistoryQuery(PageSize, Page), cancellationToken);

		Entries.Clear();
		foreach (TranscriptEntryDto entry in first)
		{
			Entries.Add(entry);
		}

		IsEmpty = Entries.Count == 0;

		// Only a completely full page can still be followed by another; a short (or empty) first page
		// already proves the history is exhausted.
		HasMorePages = first.Count == PageSize;
	}

	// Browse to the next page and append it; an empty page marks the history exhausted. Awaiting the
	// query keeps the UI thread free while the page loads.
	[RelayCommand]
	private async Task NextPageAsync(CancellationToken cancellationToken)
	{
		IReadOnlyList<TranscriptEntryDto> next = await _mediator.Send(new BrowseHistoryQuery(PageSize, Page + 1), cancellationToken);
		if (next.Count == 0)
		{
			HasMorePages = false;
			return;
		}

		Page++;
		foreach (TranscriptEntryDto entry in next)
		{
			// A live-prepended entry shifts the store's page boundary by one, so the next page
			// can repeat an entry already shown; skip any id already loaded so the list never doubles up.
			if (Entries.Any(existing => existing.Id == entry.Id))
			{
				continue;
			}

			Entries.Add(entry);
		}

		// A short page is the last page; only a completely full one can still be followed by another.
		HasMorePages = next.Count == PageSize;
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
