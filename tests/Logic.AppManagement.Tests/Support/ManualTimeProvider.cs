// A minimal, deterministic TimeProvider for unit tests: time only advances when the test calls Advance,
// and one-shot timers created via CreateTimer fire synchronously when the manual clock passes their due
// time. It implements exactly what the orchestrator's post-release grace window uses — a
// one-shot Task.Delay timer — so the capture-tail drain can be proven without real wall-clock waiting.
// The equivalent of Application.Tests' ManualTimeProvider; not a general-purpose fake (no recurring
// periods), kept tiny on purpose.

namespace Logic.AppManagement.Tests.Support;

public sealed class ManualTimeProvider : TimeProvider
{
	private readonly object _gate = new();
	private readonly List<ManualTimer> _timers = [];
	private DateTimeOffset _now = DateTimeOffset.UnixEpoch;

	// Default UTC so day-bucketing tests that don't care about the zone are deterministic and
	// match the prior UTC behavior; tests that pin a non-UTC zone call SetLocalTimeZone.
	private TimeZoneInfo _localTimeZone = TimeZoneInfo.Utc;

	public override TimeZoneInfo LocalTimeZone => _localTimeZone;

	public void SetLocalTimeZone(TimeZoneInfo zone) => _localTimeZone = zone;

	public override DateTimeOffset GetUtcNow()
	{
		lock (_gate)
		{
			return _now;
		}
	}

	// Advance the manual clock, firing every one-shot timer whose due time is now reached.
	public void Advance(TimeSpan delta)
	{
		ManualTimer[] due;
		lock (_gate)
		{
			_now += delta;
			due = _timers.Where(timer => timer.IsDue(_now)).ToArray();
			foreach (ManualTimer timer in due)
			{
				timer.Disarm();
			}
		}

		foreach (ManualTimer timer in due)
		{
			timer.Fire();
		}
	}

	public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
	{
		ManualTimer timer = new(callback, state, this);
		lock (_gate)
		{
			_timers.Add(timer);
			timer.Arm(_now, dueTime);
		}

		return timer;
	}

	private void Remove(ManualTimer timer)
	{
		lock (_gate)
		{
			_timers.Remove(timer);
		}
	}

	private sealed class ManualTimer(TimerCallback callback, object? state, ManualTimeProvider owner) : ITimer
	{
		private DateTimeOffset? _dueAt;

		public void Arm(DateTimeOffset now, TimeSpan dueTime) =>
			_dueAt = dueTime == Timeout.InfiniteTimeSpan ? null : now + dueTime;

		public bool IsDue(DateTimeOffset now) => _dueAt is { } due && due <= now;

		public void Disarm() => _dueAt = null;

		public void Fire() => callback(state);

		public bool Change(TimeSpan dueTime, TimeSpan period)
		{
			_dueAt = dueTime == Timeout.InfiniteTimeSpan ? null : owner.GetUtcNow() + dueTime;
			return true;
		}

		public void Dispose()
		{
			_dueAt = null;
			owner.Remove(this);
		}

		public ValueTask DisposeAsync()
		{
			Dispose();
			return ValueTask.CompletedTask;
		}
	}
}
