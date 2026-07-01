// A display monitor as the app needs to reason about it: a stable-ish device name to persist a
// selection against, a friendly label for the picker, whether it is the OS primary, and its work area
// (the monitor minus the taskbar) in device-independent pixels within WPF's coordinate space, so the
// overlay can be placed on it without any further DPI math. Framework-neutral (plain numbers, no WPF
// types) so it crosses the port boundary and the query result cleanly. Enumerating real monitors is a
// display/OS concern surfaced through IMonitorCatalog, implemented in the Presentation composition root.

namespace Application.Display;

public sealed record MonitorInfo(
	string DeviceName,
	string FriendlyName,
	bool IsPrimary,
	double WorkAreaLeft,
	double WorkAreaTop,
	double WorkAreaWidth,
	double WorkAreaHeight);
