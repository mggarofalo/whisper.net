// Owns the H.NotifyIcon tray icon for the app's lifetime (WHISPER-18). It builds the system-tray icon
// and its context menu (a status line, "Open Settings", and "Quit"), binds the menu to the view-model's
// relay commands, and DATA-BINDS the icon colour, tooltip, and status line to the view-model
// (WHISPER-92) — no PropertyChanged subscription, no property-name switch — so a renamed view-model
// property refactors the nameof-based paths or fails loudly. It is pure view glue over the
// TrayIconViewModel — all behaviour lives in the controller the view-model wraps — so it is verified
// by smoke, not by the specs.

using System;
using System.Windows.Controls;
using System.Windows.Data;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using Logic.AppManagement.Tray;

namespace Presentation.Tray;

public sealed class TrayIcon : IDisposable
{
	private readonly TaskbarIcon _icon;
	private readonly TrayIconViewModel _viewModel;

	public TrayIcon(TrayIconViewModel viewModel)
	{
		_viewModel = viewModel;

		MenuItem statusItem = new() { IsEnabled = false };
		statusItem.SetBinding(MenuItem.HeaderProperty,
			new Binding(nameof(TrayIconViewModel.ToolTipText)) { Source = viewModel });

		ContextMenu menu = new();
		menu.Items.Add(statusItem);
		menu.Items.Add(new Separator());
		menu.Items.Add(new MenuItem { Header = "Open Settings", Command = viewModel.OpenSettingsCommand });
		menu.Items.Add(new MenuItem { Header = "Quit", Command = viewModel.QuitCommand });

		_icon = new TaskbarIcon { ContextMenu = menu };
		_icon.SetBinding(TaskbarIcon.ToolTipTextProperty,
			new Binding(nameof(TrayIconViewModel.ToolTipText)) { Source = viewModel });
		_icon.SetBinding(TaskbarIcon.IconSourceProperty,
			new Binding(nameof(TrayIconViewModel.Status)) { Source = viewModel, Converter = new RecordingStateToIconSourceConverter() });

		_icon.ForceCreate();
	}

	/// <summary>Shows a tray balloon notification — the presenter the TrayUserNotifier attaches (WHISPER-95).</summary>
	public void ShowNotification(string title, string message) =>
		_icon.ShowNotification(title, message, NotificationIcon.Error);

	public void Dispose()
	{
		_viewModel.Dispose();
		_icon.Dispose();
	}
}
