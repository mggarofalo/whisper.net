// Unit depth for the named-mutex adapter, using a real OS mutex under a unique per-test name. A Win32
// mutex is thread-affine (and recursive for its owner), so contention is always checked from a different
// thread. Pins the shutdown contract: Release from a thread that does not own the mutex must not throw
// (the Generic Host disposes on a pool thread), Release/Dispose are idempotent, and a later instance can
// acquire whether the release was a real ReleaseMutex or an abandonment. Windows-only.

using System.Runtime.Versioning;
using AwesomeAssertions;
using Infrastructure.Lifecycle;
using Xunit;

namespace Infrastructure.Tests.Lifecycle;

[SupportedOSPlatform("windows")]
public sealed class MutexInstanceLockTests
{
	private readonly string _name = $"whisper-net-tests-{Guid.NewGuid():N}";

	[Fact]
	public void A_second_instance_cannot_acquire_while_the_first_holds_the_lock()
	{
		using MutexInstanceLock first = new(_name);
		first.TryAcquire().Should().BeTrue();

		OnOtherThread(() => TryAcquireAndDispose()).Should().BeFalse();
	}

	[Fact]
	public void Releasing_on_the_owning_thread_lets_a_later_instance_acquire()
	{
		using MutexInstanceLock first = new(_name);
		first.TryAcquire().Should().BeTrue();

		first.Release();

		OnOtherThread(() => TryAcquireAndDispose()).Should().BeTrue();
	}

	[Fact]
	public void Releasing_from_a_thread_that_does_not_own_the_mutex_does_not_throw()
	{
		using MutexInstanceLock first = new(_name);
		first.TryAcquire().Should().BeTrue();

		Exception? failure = OnOtherThread(() => Record.Exception(first.Release));

		failure.Should().BeNull("shutdown may release from a pool thread, and that must not crash the exit path");
	}

	[Fact]
	public void A_cross_thread_release_still_frees_the_lock_once_the_owning_thread_exits()
	{
		using MutexInstanceLock first = new(_name);
		OnOtherThread(first.TryAcquire).Should().BeTrue("the owning thread has now exited without releasing");

		first.Release();

		TryAcquireAndDispose().Should().BeTrue("an abandoned mutex is treated as acquired by the next launch");
	}

	[Fact]
	public void Release_and_dispose_are_idempotent()
	{
		MutexInstanceLock instanceLock = new(_name);
		instanceLock.TryAcquire().Should().BeTrue();

		Action teardown = () =>
		{
			instanceLock.Release();
			instanceLock.Release();
			instanceLock.Dispose();
			instanceLock.Dispose();
		};

		teardown.Should().NotThrow();
	}

	[Fact]
	public void Dispose_without_acquiring_does_not_throw()
	{
		MutexInstanceLock instanceLock = new(_name);

		Action dispose = instanceLock.Dispose;

		dispose.Should().NotThrow();
	}

	private bool TryAcquireAndDispose()
	{
		using MutexInstanceLock other = new(_name);
		return other.TryAcquire();
	}

	private static T OnOtherThread<T>(Func<T> action)
	{
		T result = default!;
		Thread thread = new(() => result = action());
		thread.Start();
		thread.Join();
		return result;
	}
}
