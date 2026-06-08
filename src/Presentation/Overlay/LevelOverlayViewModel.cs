// The level overlay's view-model (WHISPER-26): a thin CommunityToolkit.Mvvm wrapper over the Logic-layer
// LevelOverlayController. It exposes the overlay visibility and the current input level as observable
// properties (refreshed on the UI thread when the controller changes), which the mini-recorder window
// binds to. All real coordination — show-while-recording and the smoothed level math — lives in the
// controller, which is what the @WHISPER-26 specs exercise; this wrapper carries no logic of its own.

using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Logic.AppManagement;

namespace Presentation.Overlay;

public sealed partial class LevelOverlayViewModel : ObservableObject, IDisposable
{
	private readonly LevelOverlayController _controller;

	[ObservableProperty]
	private bool _isOverlayVisible;

	[ObservableProperty]
	private double _level;

	public LevelOverlayViewModel(LevelOverlayController controller)
	{
		_controller = controller;
		_isOverlayVisible = controller.IsVisible;
		_level = controller.Level;
		_controller.VisibilityChanged += OnVisibilityChanged;
		_controller.LevelChanged += OnLevelChanged;
	}

	private void OnVisibilityChanged(object? sender, EventArgs e) =>
		System.Windows.Application.Current.Dispatcher.Invoke(() => IsOverlayVisible = _controller.IsVisible);

	private void OnLevelChanged(object? sender, EventArgs e) =>
		System.Windows.Application.Current.Dispatcher.Invoke(() => Level = _controller.Level);

	public void Dispose()
	{
		_controller.VisibilityChanged -= OnVisibilityChanged;
		_controller.LevelChanged -= OnLevelChanged;
	}
}
