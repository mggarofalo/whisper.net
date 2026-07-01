// The shell's stats dashboard: shows the headline usage figures — total transcriptions,
// total words, and estimated time saved. It depends on nothing but IMediator and does no arithmetic of
// its own: it dispatches GetUsageStatsQuery and binds the projected totals, so all aggregation stays
// behind the Application layer (the Logic calculator). Refreshing re-runs the query, and an empty
// history yields zeroed totals rather than an error. Built on CommunityToolkit.Mvvm and WPF-free so the
// behavior is driven for real in specs; the thin view binds to it.

using Application.History;
using Application.Ports;
using Application.Statistics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Mediator;

namespace Logic.AppManagement.Shell;

public sealed partial class StatsViewModel : FeatureViewModel
{
	private readonly IMediator _mediator;
	private readonly IMessenger _messenger;
	private readonly IUiDispatcher _uiDispatcher;

	public StatsViewModel(IMediator mediator, IMessenger messenger, IUiDispatcher uiDispatcher)
	{
		_mediator = mediator;
		_messenger = messenger;
		_uiDispatcher = uiDispatcher;

		// Live totals: re-run the aggregate query whenever a transcription is recorded, for the section's
		// whole lifetime (not just while visible), so the dashboard reflects new activity the moment it
		// happens instead of going stale until a manual Refresh or a re-open. RefreshCommand disallows
		// concurrent runs, so a burst of recordings collapses to the latest re-query. The shared
		// WeakReferenceMessenger holds the recipient weakly, so this persistent registration cannot leak it.
		// The message is published on the record/background thread, so the refresh is marshalled to the UI
		// thread: RefreshCommand (an AsyncRelayCommand) raises CanExecuteChanged, and a bound Refresh button
		// firing that off the UI thread throws a cross-thread exception (WHISPER-138).
		_messenger.Register<StatsViewModel, TranscriptionRecordedMessage>(
			this, static (recipient, _) => recipient.RefreshOnUiThread());
	}

	// Run the refresh on the UI thread; the transcription-recorded message arrives on the background record
	// thread and executing RefreshCommand there raises CanExecuteChanged on the wrong thread (WHISPER-138).
	private void RefreshOnUiThread()
	{
		if (_uiDispatcher.CheckAccess())
		{
			RefreshCommand.Execute(null);
			return;
		}

		_uiDispatcher.Post(() => RefreshCommand.Execute(null));
	}

	/// <summary>Total number of dictation sessions recorded.</summary>
	[ObservableProperty]
	private int _totalTranscriptions;

	/// <summary>Total words dictated across all sessions.</summary>
	[ObservableProperty]
	private int _totalWords;

	/// <summary>Estimated typing time saved, derived by the Application layer from the word count.</summary>
	[ObservableProperty]
	private TimeSpan _estimatedTimeSaved;

	// Auto-load the totals on first activation: the dashboard opens populated, the cached
	// instance does not re-query on later tab switches, and Refresh stays the manual re-query.
	protected override IAsyncRelayCommand FirstActivationLoadCommand => RefreshCommand;

	// Re-read the aggregate usage stats through Mediator and surface the totals; Refresh re-runs it so
	// the dashboard reflects new activity.
	[RelayCommand]
	private async Task RefreshAsync(CancellationToken cancellationToken)
	{
		UsageStatsDto stats = await _mediator.Send(new GetUsageStatsQuery(From: null, To: null), cancellationToken);
		TotalTranscriptions = stats.TotalSessions;
		TotalWords = stats.TotalWords;
		EstimatedTimeSaved = stats.EstimatedTimeSaved;
	}
}
