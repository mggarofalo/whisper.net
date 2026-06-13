// Edge-case depth for the hotkey listener, beyond the acceptance scenarios.
// Drives the real EventLoopHotkeyListener over a fake hook (no OS hook) and pins down the contract the
// app depends on: the hook is pumped on a dedicated background thread that Start does not block on and
// Dispose joins cleanly; the live modifier set is tracked across presses and releases; a second Start
// does not double-subscribe; and a hook that fails to start is logged rather than crashing the host.

using System.Collections.Concurrent;
using Application.Ports;
using AwesomeAssertions;
using Domain.Input;
using Infrastructure.Hotkeys;
using Microsoft.Extensions.Logging;
using SharpHook.Data;
using Xunit;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Infrastructure.Tests.Hotkeys;

public sealed class EventLoopHotkeyListenerTests
{
	[Fact]
	public void Start_does_not_block_and_Dispose_stops_and_joins_the_pump_thread()
	{
		FakeHook hook = new();
		EventLoopHotkeyListener listener = new(hook, NullLogger());

		listener.Start();

		// Start returned despite Run() blocking, so the loop is pumped off the caller's thread.
		WaitUntil(() => hook.IsRunning).Should().BeTrue("the hook is pumped on a dedicated thread");

		listener.Dispose();

		// Dispose joined the thread: by the time it returns the loop has stopped and the hook disposed.
		hook.IsRunning.Should().BeFalse();
		hook.Disposed.Should().BeTrue();
	}

	[Fact]
	public void Each_edge_carries_the_live_modifier_set()
	{
		FakeHook hook = new();
		EventLoopHotkeyListener listener = new(hook, NullLogger());
		List<KeyboardKeyEventArgs> downs = [];
		List<KeyboardKeyEventArgs> ups = [];
		listener.KeyDown += (_, e) => downs.Add(e);
		listener.KeyUp += (_, e) => ups.Add(e);
		listener.Start();

		hook.Press(KeyCode.VcLeftControl);
		hook.Press(KeyCode.VcA);
		hook.Release(KeyCode.VcLeftControl);

		downs[0].Should().Be(new KeyboardKeyEventArgs(KeyboardKey.Control, KeyModifiers.Control));
		downs[1].Should().Be(new KeyboardKeyEventArgs(KeyboardKey.A, KeyModifiers.Control));
		ups[0].Should().Be(new KeyboardKeyEventArgs(KeyboardKey.Control, KeyModifiers.None));

		listener.Dispose();
	}

	[Fact]
	public void A_second_Start_does_not_double_subscribe()
	{
		FakeHook hook = new();
		EventLoopHotkeyListener listener = new(hook, NullLogger());
		List<KeyboardKeyEventArgs> downs = [];
		listener.KeyDown += (_, e) => downs.Add(e);

		listener.Start();
		listener.Start();
		hook.Press(KeyCode.VcA);

		downs.Should().ContainSingle();

		listener.Dispose();
	}

	[Fact]
	public void A_hook_that_fails_to_start_is_logged_and_does_not_crash_the_host()
	{
		FakeHook hook = new(throwOnRun: true);
		ListLogger logger = new();
		EventLoopHotkeyListener listener = new(hook, logger);

		Action start = listener.Start;

		// The failure happens on the pump thread, so Start itself must not throw.
		start.Should().NotThrow();

		// Dispose joins the (already-failed) pump thread; by then the error has been logged.
		listener.Dispose();
		WaitUntil(() => logger.Errors > 0).Should().BeTrue("a hook-start failure is logged, not thrown");
	}

	// --- helpers ---

	private static bool WaitUntil(Func<bool> condition)
	{
		SpinWait spin = default;
		long deadline = Environment.TickCount64 + 2_000;
		while (Environment.TickCount64 < deadline)
		{
			if (condition())
			{
				return true;
			}

			spin.SpinOnce();
		}

		return condition();
	}

	private static ILogger<EventLoopHotkeyListener> NullLogger() => new ListLogger();

	// A controllable native-hook stand-in: Run blocks until stopped (or throws on demand), and tests
	// inject raw codes through Press/Release.
	private sealed class FakeHook(bool throwOnRun = false) : IGlobalKeyHook
	{
		private readonly object _gate = new();
		private bool _stop;

		public bool IsRunning { get; private set; }
		public bool Disposed { get; private set; }

		public event EventHandler<KeyCode>? KeyPressed;
		public event EventHandler<KeyCode>? KeyReleased;

		public void Run()
		{
			if (throwOnRun)
			{
				throw new InvalidOperationException("hook failed to start");
			}

			lock (_gate)
			{
				IsRunning = true;
				while (!_stop)
				{
					Monitor.Wait(_gate);
				}

				IsRunning = false;
			}
		}

		public void Stop()
		{
			lock (_gate)
			{
				_stop = true;
				Monitor.PulseAll(_gate);
			}
		}

		public void Dispose()
		{
			Disposed = true;
			Stop();
		}

		public void Press(KeyCode code) => KeyPressed?.Invoke(this, code);

		public void Release(KeyCode code) => KeyReleased?.Invoke(this, code);
	}

	// A minimal ILogger that just counts error-level entries, for the failure-logging assertion.
	private sealed class ListLogger : ILogger<EventLoopHotkeyListener>
	{
		private readonly ConcurrentQueue<LogLevel> _levels = new();

		public int Errors => _levels.Count(level => level == LogLevel.Error);

		public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(
			LogLevel logLevel,
			EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter) => _levels.Enqueue(logLevel);

		private sealed class NullScope : IDisposable
		{
			public static readonly NullScope Instance = new();

			public void Dispose()
			{
			}
		}
	}
}
