// Unit depth for single-instance coordination beyond the acceptance scenarios. Pins the shutdown
// contract the composition root relies on: the app releases the lock explicitly on the UI thread and the
// host disposes the coordinator afterwards, so a second release must be a no-op rather than a second
// call into the OS lock.

using Application.Ports;
using AwesomeAssertions;
using Logic.AppManagement.Lifecycle;
using NSubstitute;
using Xunit;

namespace Logic.AppManagement.Tests.Lifecycle;

public sealed class SingleInstanceCoordinatorTests
{
	private readonly IInstanceLock _lock = Substitute.For<IInstanceLock>();
	private readonly IInstanceSignal _signal = Substitute.For<IInstanceSignal>();
	private readonly IShellPresenter _shell = Substitute.For<IShellPresenter>();

	[Fact]
	public void Releasing_then_disposing_releases_the_lock_exactly_once()
	{
		_lock.TryAcquire().Returns(true);
		SingleInstanceCoordinator coordinator = new(_lock, _signal, _shell);
		coordinator.TryStartAsPrimary().Should().BeTrue();

		coordinator.ReleasePrimary();
		coordinator.Dispose();

		_lock.Received(1).Release();
	}

	[Fact]
	public void A_secondary_instance_never_releases_a_lock_it_does_not_hold()
	{
		_lock.TryAcquire().Returns(false);
		SingleInstanceCoordinator coordinator = new(_lock, _signal, _shell);
		coordinator.TryStartAsPrimary().Should().BeFalse();

		coordinator.Dispose();

		_lock.DidNotReceive().Release();
	}
}
