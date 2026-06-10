// The shell's model picker (WHISPER-27, growing the WHISPER-19 representative section): it lists the
// catalog models with speed/accuracy/memory ratings, lets the user download one with live progress, and
// switches the active model on selection. It depends on nothing but IMediator — no ports, no handlers,
// no Infrastructure: it loads via ListModelsQuery, downloads via DownloadModelCommand (forwarding a
// progress sink it owns), and activates via SwitchActiveModelCommand. Selecting an un-downloaded model
// downloads it first and only activates on success. Built on CommunityToolkit.Mvvm and WPF-free so the
// behavior is driven for real in specs; the thin WPF view binds to it.

using System.Collections.ObjectModel;
using Application.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Domain.Models;
using Mediator;

namespace Logic.AppManagement.Shell;

public sealed partial class ModelViewModel : ObservableValidator, IFeatureViewModel
{
	private readonly IMediator _mediator;

	public ModelViewModel(IMediator mediator) => _mediator = mediator;

	/// <summary>The catalog models shown in the picker, one row each.</summary>
	public ObservableCollection<ModelItemViewModel> Models { get; } = [];

	/// <summary>The id of the model currently selected as active, reflected in the view.</summary>
	[ObservableProperty]
	private string? _activeModelId;

	/// <summary>A user-facing error from the last download attempt, or null when none failed. The view shows
	/// this natively rather than crashing on a failed download (WHISPER-81).</summary>
	[ObservableProperty]
	private string? _downloadError;

	/// <summary>Whether this section is the shell's active content; toggled by the navigation lifecycle.</summary>
	[ObservableProperty]
	private bool _isActive;

	public void OnNavigatedTo() => IsActive = true;

	public void OnNavigatedFrom() => IsActive = false;

	// Load the model list through Mediator and project each into a row; mark which one is active.
	[RelayCommand]
	private async Task LoadAsync(CancellationToken cancellationToken)
	{
		IReadOnlyList<ModelListItemDto> items = await _mediator.Send(new ListModelsQuery(), cancellationToken);

		Models.Clear();
		foreach (ModelListItemDto item in items)
		{
			Models.Add(new ModelItemViewModel(item));
		}

		ActiveModelId = items.FirstOrDefault(item => item.IsActive)?.Id;
	}

	// Selecting a model activates it. If it is not yet downloaded, download it first (with progress) and
	// only switch on a successful download — a failed download leaves the active model unchanged.
	[RelayCommand]
	private async Task SelectAsync(ModelItemViewModel? item, CancellationToken cancellationToken)
	{
		if (item is null)
		{
			return;
		}

		if (!item.IsDownloaded)
		{
			await DownloadAsync(item, cancellationToken);
			if (item.DownloadState != ModelDownloadState.Succeeded)
			{
				return;
			}
		}

		await _mediator.Send(new SwitchActiveModelCommand(item.Id), cancellationToken);
		SetActive(item.Id);
	}

	// Download a model through Mediator, surfacing live determinate progress and a terminal success/failure
	// state. The command is async end-to-end (no .Result/.Wait, so the UI thread never blocks), cannot run
	// concurrently with itself, and is cancelable: IncludeCancelCommand generates DownloadCancelCommand,
	// which cancels this invocation's token. IsRunning (on DownloadCommand) drives the progress bar and
	// button enablement in the view.
	[RelayCommand(IncludeCancelCommand = true, AllowConcurrentExecutions = false)]
	private async Task DownloadAsync(ModelItemViewModel? item, CancellationToken cancellationToken)
	{
		if (item is null)
		{
			return;
		}

		DownloadError = null;
		item.DownloadPercent = 0;
		item.DownloadState = ModelDownloadState.InProgress;

		// A synchronous progress sink: it updates the row inline as each report arrives. WPF marshals the
		// scalar property change to the UI thread; the specs see deterministic, ordered updates.
		IProgress<ModelDownloadProgress> progress = new InlineProgress(report =>
			item.DownloadPercent = report.Percent ?? item.DownloadPercent);

		try
		{
			await _mediator.Send(new DownloadModelCommand(item.Id, progress), cancellationToken);
			item.DownloadPercent = 100;
			item.IsDownloaded = true;
			item.DownloadState = ModelDownloadState.Succeeded;
		}
		catch (OperationCanceledException)
		{
			// The user cancelled: reset the row to its pre-download state, leaving no half-finished progress.
			item.DownloadPercent = 0;
			item.DownloadState = ModelDownloadState.NotStarted;
		}
		catch (Exception)
		{
			// A failed download is a terminal state the view surfaces natively; the active model is unchanged.
			item.DownloadState = ModelDownloadState.Failed;
			DownloadError = $"Couldn't download '{item.DisplayName}'. Check your connection and try again.";
		}
	}

	private void SetActive(string modelId)
	{
		ActiveModelId = modelId;
		foreach (ModelItemViewModel model in Models)
		{
			model.IsActive = string.Equals(model.Id, modelId, StringComparison.OrdinalIgnoreCase);
		}
	}

	// Reports progress synchronously (inline with each Report call), unlike Progress<T> which marshals to
	// a captured context and would race the specs' assertions.
	private sealed class InlineProgress(Action<ModelDownloadProgress> report) : IProgress<ModelDownloadProgress>
	{
		public void Report(ModelDownloadProgress value) => report(value);
	}
}
