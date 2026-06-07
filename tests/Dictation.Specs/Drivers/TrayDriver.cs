// Drives the @WHISPER-18 tray scenarios. It owns HOW the tray coordination is exercised so the step
// definitions stay one-liners: it builds the REAL TrayController over the REAL RecordingStateMachine,
// with the IShellPresenter and IHostApplicationLifetime seams faked. The tray icon's status, its
// "Open Settings" action, and its "Quit" graceful shutdown are validated at the controller boundary;
// the actual H.NotifyIcon view is thin Presentation glue verified by smoke.

using AwesomeAssertions;
using Dictation.Specs.Support;
using Domain.Recording;
using Logic.AppManagement;
using Logic.AppManagement.Tray;

namespace Dictation.Specs.Drivers;

public sealed class TrayDriver(
	RecordingStateMachine stateMachine,
	FakeShellPresenter shell,
	FakeApplicationLifetime lifetime) : IDisposable
{
	private TrayController? _controller;

	public void TrayIsActive() => _controller = new TrayController(stateMachine, shell, lifetime);

	public void StatusChangesToRecording() => stateMachine.RequestStart();

	public void SelectQuit() => Controller.Quit();

	public void SelectOpenSettings() => Controller.OpenSettings();

	public void AssertRecordingIndicator() =>
		Controller.Status.Should().Be(RecordingState.Recording, "the tray icon should reflect the recording status");

	public void AssertTooltipDescribesRecording() =>
		Controller.Tooltip.Should().ContainEquivalentOf("recording", "the tooltip should describe the current status");

	public void AssertGracefulShutdownRequested() =>
		lifetime.StopApplicationCalled.Should().BeTrue("Quit should trigger a graceful host shutdown");

	public void AssertSettingsShown() =>
		shell.ShowSettingsCallCount.Should().Be(1, "Open Settings should show the settings window");

	private TrayController Controller => _controller ?? throw new InvalidOperationException("TrayIsActive was not called.");

	public void Dispose() => _controller?.Dispose();
}
