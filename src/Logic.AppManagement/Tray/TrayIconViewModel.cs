// The tray icon's view-model (WHISPER-18): a thin CommunityToolkit.Mvvm wrapper over the
// TrayController. It exposes the current status and tooltip as observable properties and the two menu
// actions as relay commands the context menu binds to. Moved out of Presentation (WHISPER-90) so the
// specs and unit tests drive it for real: controller events are marshaled through the injected
// IUiDispatcher seam — with a CheckAccess fast-path — instead of a hand-rolled
// Application.Current.Dispatcher.Invoke that was untestable and null at shutdown.

using Application.Ports;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Domain.Recording;

namespace Logic.AppManagement.Tray;

public sealed partial class TrayIconViewModel : ObservableObject, IDisposable
{
	private readonly TrayController _controller;
	private readonly IUiDispatcher _dispatcher;

	[ObservableProperty]
	private RecordingState _status;

	[ObservableProperty]
	private string _toolTipText;

	public TrayIconViewModel(TrayController controller, IUiDispatcher dispatcher)
	{
		_controller = controller;
		_dispatcher = dispatcher;
		_status = controller.Status;
		_toolTipText = controller.Tooltip;
		_controller.StatusChanged += OnStatusChanged;
	}

	[RelayCommand]
	private void OpenSettings() => _controller.OpenSettings();

	[RelayCommand]
	private void Quit() => _controller.Quit();

	private void OnStatusChanged(object? sender, EventArgs e)
	{
		if (_dispatcher.CheckAccess())
		{
			ApplyStatus();
			return;
		}

		_dispatcher.Post(ApplyStatus);
	}

	private void ApplyStatus()
	{
		Status = _controller.Status;
		ToolTipText = _controller.Tooltip;
	}

	public void Dispose() => _controller.StatusChanged -= OnStatusChanged;
}
