// In-memory stand-in for the IShellPresenter port for the @WHISPER-18 / @WHISPER-25 scenarios. Records
// how many times the shell was asked to surface the settings window, so specs assert "Open Settings"
// (and, later, single-instance activation) reached the presenter without a real WPF window.

using Application.Ports;

namespace Dictation.Specs.Support;

public sealed class FakeShellPresenter : IShellPresenter
{
	public int ShowSettingsCallCount { get; private set; }

	public void ShowSettings() => ShowSettingsCallCount++;
}
