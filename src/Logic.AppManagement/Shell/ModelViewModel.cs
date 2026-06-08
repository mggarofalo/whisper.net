// The shell's model section (WHISPER-19): the representative feature view-model proving the MVVM +
// Mediator pattern the rest of M10 follows. It depends on nothing but IMediator — no ports, no
// handlers, no Infrastructure — and its one action dispatches a Mediator query (GetSettingsQuery) to
// learn the active model id, which the view binds to. WHISPER-27 grows this into the full model picker
// (ratings, download progress, switch active model); here it stays deliberately minimal so the shell
// has a real, DI-composed feature view to navigate to. Built on CommunityToolkit.Mvvm and WPF-free so
// the behavior is driven for real in specs.

using Application.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mediator;

namespace Logic.AppManagement.Shell;

public sealed partial class ModelViewModel : ObservableObject, IFeatureViewModel
{
	private readonly IMediator _mediator;

	public ModelViewModel(IMediator mediator) => _mediator = mediator;

	/// <summary>The id of the model currently selected as active, learned via the Mediator query.</summary>
	[ObservableProperty]
	private string? _activeModelId;

	/// <summary>Whether this section is the shell's active content; toggled by the navigation lifecycle.</summary>
	[ObservableProperty]
	private bool _isActive;

	public void OnNavigatedTo() => IsActive = true;

	public void OnNavigatedFrom() => IsActive = false;

	// The representative ViewModel action: read the current settings through the Mediator pipeline (no
	// direct port or Infrastructure call) and surface the active model id for the view to bind.
	[RelayCommand]
	private async Task LoadAsync(CancellationToken cancellationToken)
	{
		AppSettingsDto settings = await _mediator.Send(new GetSettingsQuery(), cancellationToken);
		ActiveModelId = settings.ModelId;
	}
}
