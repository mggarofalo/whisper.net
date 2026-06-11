// The level overlay's view-model (WHISPER-26; feedback in WHISPER-102): a thin CommunityToolkit.Mvvm
// wrapper over the LevelOverlayController, which the mini-recorder window binds to. Moved out of
// Presentation (WHISPER-90) so the specs and unit tests drive it for real: controller events are
// marshaled through the injected IUiDispatcher seam with a CheckAccess fast-path. The per-frame Level and
// the per-second Elapsed updates use the non-blocking Post path — the audio/timer threads must never block
// on the UI thread for a refresh. It surfaces the controller's presentation State, formatted elapsed
// time, and near-cap warning for the view to restyle within the existing overlay footprint.

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

	/// <summary>The presentation state the overlay shows: recording, transcribing, or error.</summary>
	[ObservableProperty]
	private OverlayState _state;

	/// <summary>The current recording's elapsed time, formatted m:ss for display.</summary>
	[ObservableProperty]
	private string _elapsedText = "0:00";

	/// <summary>True when the recording has neared/reached the duration cap — the view warns the user.</summary>
	[ObservableProperty]
	private bool _isNearCap;

	public LevelOverlayViewModel(LevelOverlayController controller, IUiDispatcher dispatcher)
	{
		_controller = controller;
		_dispatcher = dispatcher;
		_isOverlayVisible = controller.IsVisible;
		_level = controller.Level;
		_state = controller.State;
		_isNearCap = controller.NearCap;
		_elapsedText = Format(controller.Elapsed);
		_controller.VisibilityChanged += OnVisibilityChanged;
		_controller.LevelChanged += OnLevelChanged;
		_controller.StateChanged += OnStateChanged;
		_controller.ElapsedChanged += OnElapsedChanged;
	}

	private void OnVisibilityChanged(object? sender, EventArgs e) =>
		RunOnUiThread(() => IsOverlayVisible = _controller.IsVisible);

	private void OnLevelChanged(object? sender, EventArgs e) =>
		RunOnUiThread(() => Level = _controller.Level);

	private void OnStateChanged(object? sender, EventArgs e) =>
		RunOnUiThread(() =>
		{
			State = _controller.State;
			IsNearCap = _controller.NearCap;
		});

	private void OnElapsedChanged(object? sender, EventArgs e) =>
		RunOnUiThread(() => ElapsedText = Format(_controller.Elapsed));

	private static string Format(TimeSpan elapsed) =>
		$"{(int)elapsed.TotalMinutes}:{elapsed.Seconds:D2}";

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
		_controller.StateChanged -= OnStateChanged;
		_controller.ElapsedChanged -= OnElapsedChanged;
	}
}
