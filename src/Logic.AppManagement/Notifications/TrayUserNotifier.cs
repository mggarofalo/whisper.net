// The production IUserNotifier, kept WPF-free so the failure-surfacing behavior is driven
// for real in specs: notifications marshal to the UI thread through the IUiDispatcher seam (CheckAccess
// fast-path, non-blocking Post otherwise) and are presented by a balloon delegate the composition root
// attaches once the tray icon exists. Degradation is graceful by contract — no presenter attached
// (headless host, early startup) or a presenter failure (notifications suppressed by the OS) is logged
// and swallowed, never thrown: surfacing one failure must not be able to cause another.

using Application.Ports;
using Microsoft.Extensions.Logging;

namespace Logic.AppManagement.Notifications;

public sealed class TrayUserNotifier(IUiDispatcher dispatcher, ILogger<TrayUserNotifier> logger) : IUserNotifier
{
	private Action<string, string>? _presenter;

	/// <summary>Attaches the balloon presenter (the tray icon's notification call) once it exists.</summary>
	public void AttachPresenter(Action<string, string> presenter) => _presenter = presenter;

	public void NotifyError(string title, string message)
	{
		if (dispatcher.CheckAccess())
		{
			Present(title, message);
			return;
		}

		dispatcher.Post(() => Present(title, message));
	}

	private void Present(string title, string message)
	{
		if (_presenter is null)
		{
			logger.LogWarning("User notification suppressed (no balloon presenter attached): {Title}: {Message}", title, message);
			return;
		}

		try
		{
			_presenter(title, message);
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Failed to show user notification '{Title}'; continuing.", title);
		}
	}
}
