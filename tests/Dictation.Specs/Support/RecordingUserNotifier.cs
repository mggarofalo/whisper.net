// A recording IUserNotifier: captures every error notice a failure path
// requests, so specs assert a backend failure surfaced a user-visible notification without a tray icon.
// Registered scoped per scenario, overriding the production TrayUserNotifier mapping.

using Application.Ports;

namespace Dictation.Specs.Support;

public sealed class RecordingUserNotifier : IUserNotifier
{
	public List<(string Title, string Message)> Notifications { get; } = [];

	public void NotifyError(string title, string message) => Notifications.Add((title, message));
}
