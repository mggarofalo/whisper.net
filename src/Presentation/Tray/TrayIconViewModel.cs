// The tray icon's view-model (WHISPER-18): a thin CommunityToolkit.Mvvm wrapper over the Logic-layer
// TrayController. It exposes the current status and tooltip as observable properties (refreshed on the
// UI thread when the controller's status changes) and the two menu actions as relay commands the
// context menu binds to. All real coordination — status mapping, Open Settings, graceful Quit — lives
// in the controller, which is what the @WHISPER-18 specs exercise.

using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Domain.Recording;
using Logic.AppManagement.Tray;

namespace Presentation.Tray;

public sealed partial class TrayIconViewModel : ObservableObject, IDisposable
{
	private readonly TrayController _controller;

	[ObservableProperty]
	private RecordingState _status;

	[ObservableProperty]
	private string _toolTipText;

	public TrayIconViewModel(TrayController controller)
	{
		_controller = controller;
		_status = controller.Status;
		_toolTipText = controller.Tooltip;
		_controller.StatusChanged += OnStatusChanged;
	}

	[RelayCommand]
	private void OpenSettings() => _controller.OpenSettings();

	[RelayCommand]
	private void Quit() => _controller.Quit();

	private void OnStatusChanged(object? sender, EventArgs e) =>
		System.Windows.Application.Current.Dispatcher.Invoke(() =>
		{
			Status = _controller.Status;
			ToolTipText = _controller.Tooltip;
		});

	public void Dispose() => _controller.StatusChanged -= OnStatusChanged;
}
