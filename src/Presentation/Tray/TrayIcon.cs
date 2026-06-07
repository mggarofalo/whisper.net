// Owns the H.NotifyIcon tray icon for the app's lifetime (WHISPER-18). It builds the system-tray icon
// and its context menu (a status line, "Open Settings", and "Quit"), binds the menu to the view-model's
// relay commands, and refreshes the icon colour and tooltip whenever the dictation status changes. It
// is pure view glue over the TrayIconViewModel — all behaviour lives in the controller the view-model
// wraps — so it is verified by smoke, not by the specs.

using System;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media;
using Domain.Recording;
using H.NotifyIcon;

namespace Presentation.Tray;

public sealed class TrayIcon : IDisposable
{
	private readonly TaskbarIcon _icon;
	private readonly TrayIconViewModel _viewModel;
	private readonly MenuItem _statusItem;

	public TrayIcon(TrayIconViewModel viewModel)
	{
		_viewModel = viewModel;
		_statusItem = new MenuItem { Header = viewModel.ToolTipText, IsEnabled = false };

		ContextMenu menu = new();
		menu.Items.Add(_statusItem);
		menu.Items.Add(new Separator());
		menu.Items.Add(new MenuItem { Header = "Open Settings", Command = viewModel.OpenSettingsCommand });
		menu.Items.Add(new MenuItem { Header = "Quit", Command = viewModel.QuitCommand });

		_icon = new TaskbarIcon
		{
			ToolTipText = viewModel.ToolTipText,
			IconSource = StatusIcon(viewModel.Status),
			ContextMenu = menu,
		};

		_viewModel.PropertyChanged += OnViewModelPropertyChanged;
		_icon.ForceCreate();
	}

	private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		_icon.ToolTipText = _viewModel.ToolTipText;
		_icon.IconSource = StatusIcon(_viewModel.Status);
		_statusItem.Header = _viewModel.ToolTipText;
	}

	// A simple coloured dot whose colour reflects the dictation status (idle grey, recording red,
	// transcribing orange) — drawn by H.NotifyIcon, so no icon asset ships with the app.
	private static GeneratedIconSource StatusIcon(RecordingState status) => new()
	{
		Text = "●",
		Foreground = new SolidColorBrush(status switch
		{
			RecordingState.Recording => Colors.Red,
			RecordingState.Transcribing => Colors.Orange,
			_ => Colors.Gray,
		}),
	};

	public void Dispose()
	{
		_viewModel.PropertyChanged -= OnViewModelPropertyChanged;
		_viewModel.Dispose();
		_icon.Dispose();
	}
}
