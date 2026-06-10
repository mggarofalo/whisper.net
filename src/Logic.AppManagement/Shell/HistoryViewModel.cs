// The shell's history browser (WHISPER-45): lists past transcriptions newest-first, pages through them,
// and re-copies an entry to the clipboard. It reads via BrowseHistoryQuery (a page at a time, so a
// large history never blocks) and copies via CopyToClipboardCommand. Empty history is a first-class
// empty state, not an error, and HasMorePages (WHISPER-110) tells the view when Load More can no
// longer produce anything so the control disables instead of silently no-opping. Built on CommunityToolkit.Mvvm and WPF-free so the behavior is driven for
// real in specs; the thin view binds to it. Entries is a UiBoundCollection registered through the
// collection-sync seam at construction (WHISPER-91), so a future off-UI-thread mutation (live feed,
// background load) binds safely instead of throwing.

using Application.History;
using Application.Ports;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mediator;

namespace Logic.AppManagement.Shell;

public sealed partial class HistoryViewModel : FeatureViewModel
{
	private const int PageSize = 50;

	private readonly IMediator _mediator;

	public HistoryViewModel(IMediator mediator, IUiCollectionSynchronizer synchronizer)
	{
		_mediator = mediator;
		synchronizer.Enable(Entries);
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
