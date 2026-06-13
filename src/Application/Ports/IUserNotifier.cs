// The user-visible error-surfacing seam. In a windowless tray app a backend failure that
// only reaches Serilog is invisible — the user just sees nothing typed. Failure paths route a short,
// non-technical notice through this port (the production implementation shows a tray balloon); the
// technical record still goes to the log. Implementations NEVER throw: surfacing a failure must not be
// able to create a second one.

namespace Application.Ports;

public interface IUserNotifier
{
	/// <summary>Surfaces a non-technical error notice to the user. Must never throw.</summary>
	void NotifyError(string title, string message);
}
