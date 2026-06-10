// The shell's history browser (WHISPER-45): lists past transcriptions newest-first, pages through them,
// and re-copies an entry to the clipboard. It depends on nothing but IMediator — it reads via
// BrowseHistoryQuery (a page at a time, so a large history never blocks) and copies via
// CopyToClipboardCommand. Empty history is a first-class empty state, not an error. Built on
// CommunityToolkit.Mvvm and WPF-free so the behavior is driven for real in specs; the thin view binds to it.

using System.Collections.ObjectModel;
using Application.History;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mediator;

namespace Logic.AppManagement.Shell;

public sealed partial class HistoryViewModel : ObservableValidator, IFeatureViewModel
{
	private const int PageSize = 50;

	private readonly IMediator _mediator;

	public HistoryViewModel(IMediator mediator) => _mediator = mediator;

	/// <summary>The loaded history entries, newest first, growing as further pages are browsed.</summary>
	public ObservableCollection<TranscriptEntryDto> Entries { get; } = [];

	/// <summary>True when there is no history to show — the view renders an empty state, not an error.</summary>
	[ObservableProperty]
	private bool _isEmpty;

	/// <summary>The 1-based index of the most recent page loaded.</summary>
	[ObservableProperty]
	private int _page = 1;

	[ObservableProperty]
	private bool _isActive;

	public void OnNavigatedTo() => IsActive = true;

	public void OnNavigatedFrom() => IsActive = false;

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
	}

	// Browse to the next page and append it; a no-op once the history is exhausted. Awaiting the query
	// keeps the UI thread free while the page loads.
	[RelayCommand]
	private async Task NextPageAsync(CancellationToken cancellationToken)
	{
		IReadOnlyList<TranscriptEntryDto> next = await _mediator.Send(new BrowseHistoryQuery(PageSize, Page + 1), cancellationToken);
		if (next.Count == 0)
		{
			return;
		}

		Page++;
		foreach (TranscriptEntryDto entry in next)
		{
			Entries.Add(entry);
		}
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
