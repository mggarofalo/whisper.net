// The shell's landing section (WHISPER-19), now a live status dashboard (WHISPER-106). It no longer sits
// empty: on first activation it composes existing read queries through IMediator into an at-a-glance
// overview the other sections don't give in one place — the active model, the dictation hotkey, the
// selected input device, headline usage totals, and the most recent transcriptions. It depends on
// nothing but IMediator (no ports, no Infrastructure) and does no arithmetic of its own — the usage
// figures are the Application layer's. Built on CommunityToolkit.Mvvm and WPF-free so the behavior is
// driven for real in specs; the thin view binds to it. Recent is a UiBoundCollection registered through
// the collection-sync seam (WHISPER-91) so a future off-UI-thread refresh binds safely.

using Application.Audio;
using Application.History;
using Application.Ports;
using Application.Settings;
using Application.Statistics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Domain.Audio;
using Mediator;

namespace Logic.AppManagement.Shell;

public sealed partial class HomeViewModel : FeatureViewModel
{
	// The dashboard shows a short glance of recent activity, not the full log (that is the History section).
	private const int RecentCount = 5;

	private readonly IMediator _mediator;

	public HomeViewModel(IMediator mediator, IUiCollectionSynchronizer synchronizer)
	{
		_mediator = mediator;
		synchronizer.Enable(Recent);
	}

	/// <summary>The id of the model dictation will load, from settings.</summary>
	[ObservableProperty]
	private string? _activeModel;

	/// <summary>The current dictation hotkey chord, from settings.</summary>
	[ObservableProperty]
	private string? _hotkey;

	/// <summary>The friendly name of the selected capture device (or "System default").</summary>
	[ObservableProperty]
	private string? _inputDevice;

	/// <summary>Total dictation sessions recorded (from the usage stats query).</summary>
	[ObservableProperty]
	private int _totalTranscriptions;

	/// <summary>Total words dictated across all sessions.</summary>
	[ObservableProperty]
	private int _totalWords;

	/// <summary>Estimated typing time saved, derived by the Application layer.</summary>
	[ObservableProperty]
	private TimeSpan _estimatedTimeSaved;

	/// <summary>The most recent transcriptions, newest first — a glance, not the full History log.</summary>
	public UiBoundCollection<TranscriptEntryDto> Recent { get; } = [];

	/// <summary>True when there are no transcriptions yet — the dashboard shows a first-class empty state.</summary>
	[ObservableProperty]
	private bool _isEmpty;

	// Refresh the overview on EVERY activation (WHISPER-119). Unlike the data sections — which load once and
	// keep their browsed page/scroll across tab switches (WHISPER-108) — the dashboard is a live overview,
	// so opening Home always re-queries settings/usage/history rather than showing a stale snapshot (e.g.
	// last night's most-recent transcription). RefreshCommand disallows concurrent runs, so a rapid
	// re-activation while a refresh is in flight is a no-op, never a duplicate query.
	protected override void OnActivated() => RefreshCommand.Execute(null);

	// Compose the overview from existing read queries; Refresh re-runs it so the dashboard reflects new
	// activity. Awaiting each query keeps the UI thread free.
	[RelayCommand]
	private async Task RefreshAsync(CancellationToken cancellationToken)
	{
		AppSettingsDto settings = await _mediator.Send(new GetSettingsQuery(), cancellationToken);
		ActiveModel = settings.ModelId;
		Hotkey = settings.Hotkey;
		InputDevice = await ResolveDeviceNameAsync(settings.CaptureDeviceId, cancellationToken);

		UsageStatsDto stats = await _mediator.Send(new GetUsageStatsQuery(From: null, To: null), cancellationToken);
		TotalTranscriptions = stats.TotalSessions;
		TotalWords = stats.TotalWords;
		EstimatedTimeSaved = stats.EstimatedTimeSaved;

		IReadOnlyList<TranscriptEntryDto> recent = await _mediator.Send(new BrowseHistoryQuery(RecentCount, 1), cancellationToken);
		Recent.Clear();
		foreach (TranscriptEntryDto entry in recent)
		{
			Recent.Add(entry);
		}

		IsEmpty = Recent.Count == 0;
	}

	// Map the persisted device id to a friendly name for display; "follow system default" needs no lookup,
	// and a device that is no longer present falls back to the same label rather than showing a raw id.
	private async Task<string> ResolveDeviceNameAsync(string captureDeviceId, CancellationToken cancellationToken)
	{
		if (string.Equals(captureDeviceId, AudioDevice.SystemDefault, StringComparison.OrdinalIgnoreCase))
		{
			return "System default";
		}

		IReadOnlyList<AudioDeviceDto> devices = await _mediator.Send(new ListCaptureDevicesQuery(), cancellationToken);
		return devices.FirstOrDefault(device => string.Equals(device.Id, captureDeviceId, StringComparison.OrdinalIgnoreCase))?.Name
			?? "System default";
	}
}
