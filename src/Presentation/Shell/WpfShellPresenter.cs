// IShellPresenter for the WPF shell (WHISPER-18, dashboard shell in WHISPER-19): shows — or focuses, if
// already open — the single dashboard window, marshaled onto the UI thread through the IUiDispatcher
// seam (WHISPER-90) rather than a hand-rolled call against the live application's dispatcher. Both the tray "Open
// Settings" action and (WHISPER-25) single-instance activation surface the window through this one seam.
// The shell view-model graph depends on the scoped Mediator, so the presenter owns one long-lived UI
// scope it resolves the view-model from — mirroring how the orchestrator runs inside a single host
// scope — and disposes it on shutdown.

using System;
using System.Linq;
using System.Windows;
using Application.Ports;
using Logic.AppManagement.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace Presentation.Shell;

public sealed class WpfShellPresenter(IServiceScopeFactory scopeFactory, IUiDispatcher uiDispatcher) : IShellPresenter, IDisposable
{
	private IServiceScope? _uiScope;

	public void ShowSettings()
	{
		if (uiDispatcher.CheckAccess())
		{
			ShowOrActivateWindow();
			return;
		}

		uiDispatcher.Post(ShowOrActivateWindow);
	}

	private void ShowOrActivateWindow()
	{
		System.Windows.Application application = System.Windows.Application.Current;
		ShellWindow window = application.Windows.OfType<ShellWindow>().FirstOrDefault() ?? CreateWindow();

		window.Show();
		if (window.WindowState == WindowState.Minimized)
		{
			window.WindowState = WindowState.Normal;
		}

		window.Activate();
	}

	private ShellWindow CreateWindow()
	{
		// One long-lived UI scope owns the shell view-model graph (and the scoped Mediator the feature
		// view-models depend on), so the dashboard never resolves a scoped service from the root provider.
		_uiScope ??= scopeFactory.CreateScope();
		return new ShellWindow(_uiScope.ServiceProvider.GetRequiredService<ShellViewModel>());
	}

	public void Dispose() => _uiScope?.Dispose();
}
