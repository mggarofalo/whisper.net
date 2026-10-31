// The level overlay's view-model: a thin CommunityToolkit.Mvvm wrapper over the
// LevelOverlayController, which the mini-recorder window binds to. Moved out of
// Presentation so the specs and unit tests drive it for real: controller events are
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

	/// <summary>The muted-mic hint or the no-audio message the pill shows in place of its meter, or null.</summary>
	[ObservableProperty]
	private string? _message;

	/// <summary>
	/// Whether the pill shows its level meter: not while warming (no input yet), and not while a message
	/// takes the meter's place in the single row.
	/// </summary>
	[ObservableProperty]
	private bool _showsLevel;

	/// <summary>Whether the pill shows the elapsed time: only while recording, and not while a message shows.</summary>
	[ObservableProperty]
	private bool _showsElapsed;

	public LevelOverlayViewModel(LevelOverlayController controller, IUiDispatcher dispatcher)
	{
		_controller = controller;
		_dispatcher = dispatcher;
		_isOverlayVisible = controller.IsVisible;
		_level = controller.Level;
		ApplyState();
		_elapsedText = Format(controller.Elapsed);
		_controller.VisibilityChanged += OnVisibilityChanged;
		_controller.LevelChanged += OnLevelChanged;
		_controller.StateChanged += OnStateChanged;
		_controller.ElapsedChanged += OnElapsedChanged;
		_controller.PresentRequested += OnPresentRequested;
	}

	/// <summary>
	/// Raised on the UI thread every time a dictation takes over the pill, so the window re-presents and
	/// verifies itself even when its visibility did not change (WHISPER-143). Ordered after the visibility
	/// update the same controller action posted, so the window is already shown when this arrives.
	/// </summary>
	public event EventHandler? PresentRequested;

	private void OnPresentRequested(object? sender, EventArgs e) =>
		RunOnUiThread(() => PresentRequested?.Invoke(this, EventArgs.Empty));

	private void OnVisibilityChanged(object? sender, EventArgs e) =>
		RunOnUiThread(() => IsOverlayVisible = _controller.IsVisible);

	private void OnLevelChanged(object? sender, EventArgs e) =>
		RunOnUiThread(() => Level = _controller.Level);

	private void OnStateChanged(object? sender, EventArgs e) =>
		RunOnUiThread(ApplyState);

	// The pill is one fixed row (WHISPER-145): the dot, then either the meter + elapsed time, the
	// "Warming up…" label, or a message in the meter's place — never a second line.
	private void ApplyState()
	{
		State = _controller.State;
		IsNearCap = _controller.NearCap;
		Message = _controller.Message;
		ShowsLevel = State is not (OverlayState.Warming or OverlayState.NoAudio) && Message is null;
		ShowsElapsed = State == OverlayState.Recording && Message is null;
	}

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
		_controller.PresentRequested -= OnPresentRequested;
	}
}
