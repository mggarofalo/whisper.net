// The shell's stats dashboard: shows the headline usage figures — total transcriptions,
// total words, and estimated time saved. It depends on nothing but IMediator and does no arithmetic of
// its own: it dispatches GetUsageStatsQuery and binds the projected totals, so all aggregation stays
// behind the Application layer (the Logic calculator). Refreshing re-runs the query, and an empty
// history yields zeroed totals rather than an error. Built on CommunityToolkit.Mvvm and WPF-free so the
// behavior is driven for real in specs; the thin view binds to it.

using Application.Statistics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mediator;

namespace Logic.AppManagement.Shell;

public sealed partial class StatsViewModel : FeatureViewModel
{
	private readonly IMediator _mediator;

	public StatsViewModel(IMediator mediator) => _mediator = mediator;

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
