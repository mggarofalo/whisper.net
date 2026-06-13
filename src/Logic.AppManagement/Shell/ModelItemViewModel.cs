// One row in the model picker: a model's identity, size, and ratings (immutable, from the
// ListModelsQuery projection) plus the live, observable state the picker mutates — whether it is
// downloaded, whether it is the active model, and its download progress/outcome. Built on
// CommunityToolkit.Mvvm and WPF-free so the picker behavior is driven for real in specs.
//
// Each row OWNS its download: the Download/Cancel commands, progress, terminal state, and
// any error live here, not on the section. So several rows can download at once, each with its own
// IsRunning, progress bar, and Cancel — starting one neither blocks, disables, nor cancels another. The
// row talks only through IMediator (DownloadModelCommand); no ports, no Infrastructure.

using Application.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Domain.Models;
using Mediator;

namespace Logic.AppManagement.Shell;

public sealed partial class ModelItemViewModel : ObservableObject
{
	private readonly IMediator _mediator;

	public ModelItemViewModel(ModelListItemDto dto, IMediator mediator)
	{
		_mediator = mediator;
		Id = dto.Id;
		DisplayName = dto.DisplayName;
		SizeBytes = dto.SizeBytes;
		Speed = dto.Speed;
		Accuracy = dto.Accuracy;
		Memory = dto.Memory;
		_isDownloaded = dto.IsDownloaded;
		_isActive = dto.IsActive;
	}

	public string Id { get; }

	public string DisplayName { get; }

	public long SizeBytes { get; }

	public ModelRating Speed { get; }

	public ModelRating Accuracy { get; }

	public ModelRating Memory { get; }

	/// <summary>Whether the model file is already in the local cache.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(CanDownload))]
	[NotifyPropertyChangedFor(nameof(CanSelect))]
	private bool _isDownloaded;

	/// <summary>Whether this is the currently active model.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(CanSelect))]
	private bool _isActive;

	/// <summary>Live download completion in [0, 100]; meaningful while <see cref="DownloadState"/> is in progress.</summary>
	[ObservableProperty]
	private double _downloadPercent;

	/// <summary>The terminal-aware state of this row's download.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(CanDownload))]
	[NotifyPropertyChangedFor(nameof(IsDownloading))]
	private ModelDownloadState _downloadState;

	// Only the action that fits this row's state is shown, so the list is compact rather than
	// a permanent three-button strip. These are derived from the observable state above and re-raise via
	// NotifyPropertyChangedFor as it changes, so the view swaps the visible action live.

	/// <summary>Show Download only when the model is not cached and not currently downloading.</summary>
	public bool CanDownload => !IsDownloaded && DownloadState != ModelDownloadState.InProgress;

	/// <summary>Show Cancel (and the progress bar) only while this row is downloading.</summary>
	public bool IsDownloading => DownloadState == ModelDownloadState.InProgress;

	/// <summary>Show Select only when the model is downloaded but not already the active one (the active row
	/// is indicated instead of offering a redundant Select).</summary>
	public bool CanSelect => IsDownloaded && !IsActive;

	/// <summary>A user-facing error from THIS row's last download attempt, or null when none failed. The view
	/// shows this per row rather than crashing on a failed download, and per row so one model's
	/// failure never masks another's progress.</summary>
	[ObservableProperty]
	private string? _downloadError;

	// Download this model through Mediator, surfacing live determinate progress and a terminal
	// success/failure state. The command is async end-to-end (no .Result/.Wait, so the UI thread never
	// blocks). AllowConcurrentExecutions = false stops THIS row from double-downloading (CanExecute is
	// false while its own download is in flight) without touching any other row — concurrency across rows
	// is exactly what's wanted. IncludeCancelCommand generates DownloadCancelCommand, which
	// cancels this row's invocation only. IsRunning (on DownloadCommand) drives this row's progress bar
	// and button enablement in the view.
	[RelayCommand(IncludeCancelCommand = true, AllowConcurrentExecutions = false)]
	private async Task DownloadAsync(CancellationToken cancellationToken)
	{
		DownloadError = null;
		DownloadPercent = 0;
		DownloadState = ModelDownloadState.InProgress;

		// A synchronous progress sink: it updates this row inline as each report arrives. WPF marshals the
		// scalar property change to the UI thread; the specs see deterministic, ordered updates.
		IProgress<ModelDownloadProgress> progress = new InlineProgress(report =>
			DownloadPercent = report.Percent ?? DownloadPercent);

		try
		{
			await _mediator.Send(new DownloadModelCommand(Id, progress), cancellationToken);
			DownloadPercent = 100;
			IsDownloaded = true;
			DownloadState = ModelDownloadState.Succeeded;
		}
		catch (OperationCanceledException)
		{
			// The user cancelled: reset this row to its pre-download state, leaving no half-finished progress.
			DownloadPercent = 0;
			DownloadState = ModelDownloadState.NotStarted;
		}
		catch (Exception)
		{
			// A failed download is a terminal state the view surfaces natively; the active model is unchanged.
			DownloadState = ModelDownloadState.Failed;
			DownloadError = $"Couldn't download '{DisplayName}'. Check your connection and try again.";
		}
	}

	// Reports progress synchronously (inline with each Report call), unlike Progress<T> which marshals to
	// a captured context and would race the specs' assertions.
	private sealed class InlineProgress(Action<ModelDownloadProgress> report) : IProgress<ModelDownloadProgress>
	{
		public void Report(ModelDownloadProgress value) => report(value);
	}
}
