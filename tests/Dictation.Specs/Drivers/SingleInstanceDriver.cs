// Drives the single-instance scenarios. It owns HOW the coordination is exercised so the
// step definitions stay one-liners: it builds REAL SingleInstanceCoordinators over a single shared
// fake lock + signal (modelling the OS-global mutex and cross-process activation) and the shared fake
// shell presenter. A "launch" is a new coordinator calling TryStartAsPrimary on that shared backing,
// exactly as a separate process would contend for the global resources.

using AwesomeAssertions;
using Dictation.Specs.Support;
using Logic.AppManagement.Lifecycle;

namespace Dictation.Specs.Drivers;

public sealed class SingleInstanceDriver(FakeShellPresenter shell) : IDisposable
{
	private readonly FakeInstanceLock _lock = new();
	private readonly FakeInstanceSignal _signal = new();

	private SingleInstanceCoordinator? _primary;
	private bool? _latestLaunchStartedPrimary;

	public void AnInstanceIsAlreadyRunning()
	{
		_primary = NewCoordinator();
		_primary.TryStartAsPrimary().Should().BeTrue("the first launch should become the primary instance");
	}

	public void APreviousInstanceShutDownGracefully()
	{
		SingleInstanceCoordinator first = NewCoordinator();
		first.TryStartAsPrimary();
		first.ReleasePrimary(); // graceful shutdown releases the lock
	}

	public void LaunchAgain() => _latestLaunchStartedPrimary = NewCoordinator().TryStartAsPrimary();

	public void LaunchApplication() => _latestLaunchStartedPrimary = NewCoordinator().TryStartAsPrimary();

	public void AssertSecondExitedWithoutStarting() =>
		_latestLaunchStartedPrimary.Should().BeFalse("a second launch must not start a new instance");

	public void AssertExistingBroughtToForeground() =>
		shell.ShowSettingsCallCount.Should().Be(1, "the running instance should surface to the user");

	public void AssertStartedAsSoleInstance() =>
		_latestLaunchStartedPrimary.Should().BeTrue("with no instance running, the launch becomes the sole instance");

	private SingleInstanceCoordinator NewCoordinator() => new(_lock, _signal, shell);

	public void Dispose() => _primary?.Dispose();
}
