// Presentation composition root. This is the WPF Application shell only — the Generic Host that
// wires every layer together (Serilog, configuration, per-layer DI extensions) is introduced in
// WHISPER-57, and the tray/overlay UI in M6. For now the shell starts and immediately exits so the
// skeleton is buildable and harmless to run.

using System.Windows;

namespace Presentation;

public partial class App : Application
{
	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);

		// No host or UI wired yet (WHISPER-57 / M6). Exit cleanly rather than sit with no window.
		Shutdown();
	}
}
