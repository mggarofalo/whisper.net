// The level overlay's view-model (WHISPER-26): a thin CommunityToolkit.Mvvm wrapper over the
// LevelOverlayController, which the mini-recorder window binds to. Moved out of Presentation
// (WHISPER-90) so the specs and unit tests drive it for real: controller events are marshaled through
// the injected IUiDispatcher seam with a CheckAccess fast-path. Both handlers use the non-blocking
// Post path — LevelChanged fires per audio frame, and the audio thread must never block on the UI
// thread for a meter refresh.

using Application.Ports;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Logic.AppManagement;

public sealed partial class LevelOverlayViewModel : ObservableObject, IDisposable
{
	private readonly LevelOverlayController _controller;
	private readonly IUiDispatcher _dispatcher;

	[ObservableProperty]
	private bool _isOverlayVisible;

	[ObservableProperty]
	private double _level;

	public LevelOverlayViewModel(LevelOverlayController controller, IUiDispatcher dispatcher)
	{
		_controller = controller;
		_dispatcher = dispatcher;
		_isOverlayVisible = controller.IsVisible;
		_level = controller.Level;
		_controller.VisibilityChanged += OnVisibilityChanged;
		_controller.LevelChanged += OnLevelChanged;
	}

	private void OnVisibilityChanged(object? sender, EventArgs e) =>
		RunOnUiThread(() => IsOverlayVisible = _controller.IsVisible);

	private void OnLevelChanged(object? sender, EventArgs e) =>
		RunOnUiThread(() => Level = _controller.Level);

	private void RunOnUiThread(Action update)
	{
		if (_dispatcher.CheckAccess())
		{
			update();
			return;
		}

		_dispatcher.Post(update);
	}

	public void Dispose()
	{
		_controller.VisibilityChanged -= OnVisibilityChanged;
		_controller.LevelChanged -= OnLevelChanged;
	}
}
