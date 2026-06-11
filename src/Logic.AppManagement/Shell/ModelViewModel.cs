// The shell's model picker (WHISPER-27, growing the WHISPER-19 representative section): it lists the
// catalog models with speed/accuracy/memory ratings, lets the user download them with live progress, and
// switches the active model on selection. It depends on nothing but IMediator — no ports, no handlers,
// no Infrastructure: it loads via ListModelsQuery and activates via SwitchActiveModelCommand. Downloads
// are owned per row (WHISPER-107): each ModelItemViewModel has its own Download/Cancel command and state,
// so several models can download concurrently; this section coordinates only the list and the active
// model. Selecting an un-downloaded model drives that row's download first and only activates on success.
// Built on CommunityToolkit.Mvvm and WPF-free so the behavior is driven for real in specs; the thin WPF
// view binds to it.

using System.Collections.ObjectModel;
using Application.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mediator;

namespace Logic.AppManagement.Shell;

public sealed partial class ModelViewModel : FeatureViewModel
{
	private readonly IMediator _mediator;

	public ModelViewModel(IMediator mediator) => _mediator = mediator;

	/// <summary>The catalog models shown in the picker, one row each.</summary>
	public ObservableCollection<ModelItemViewModel> Models { get; } = [];

	/// <summary>The id of the model currently selected as active, reflected in the view.</summary>
	[ObservableProperty]
	private string? _activeModelId;

	// Auto-load the catalog on first activation (WHISPER-108): the section opens populated, the cached
	// instance does not re-query on later tab switches, and Refresh stays the manual re-query.
	protected override IAsyncRelayCommand FirstActivationLoadCommand => LoadCommand;

	// Load the model list through Mediator and project each into a row; mark which one is active. Each row
	// gets the mediator so it can own its download (WHISPER-107).
	[RelayCommand]
	private async Task LoadAsync(CancellationToken cancellationToken)
	{
		IReadOnlyList<ModelListItemDto> items = await _mediator.Send(new ListModelsQuery(), cancellationToken);

		Models.Clear();
		foreach (ModelListItemDto item in items)
		{
			Models.Add(new ModelItemViewModel(item, _mediator));
		}

		ActiveModelId = items.FirstOrDefault(item => item.IsActive)?.Id;
	}

	// Selecting a model activates it. If it is not yet downloaded, drive that row's own download first (with
	// progress) and only switch on a successful download — a failed download leaves the active model
	// unchanged. The download lives on the row (WHISPER-107); this only coordinates the active switch.
	[RelayCommand]
	private async Task SelectAsync(ModelItemViewModel? item, CancellationToken cancellationToken)
	{
		if (item is null)
		{
			return;
		}

		if (!item.IsDownloaded)
		{
			await item.DownloadCommand.ExecuteAsync(null);
			if (item.DownloadState != ModelDownloadState.Succeeded)
			{
				return;
			}
		}

		await _mediator.Send(new SwitchActiveModelCommand(item.Id), cancellationToken);
		SetActive(item.Id);
	}

	private void SetActive(string modelId)
	{
		ActiveModelId = modelId;
		foreach (ModelItemViewModel model in Models)
		{
			model.IsActive = string.Equals(model.Id, modelId, StringComparison.OrdinalIgnoreCase);
		}
	}
}
