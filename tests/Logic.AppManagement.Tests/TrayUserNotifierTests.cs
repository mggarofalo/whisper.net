// Unit depth for the WHISPER-95 user notifier, beyond the @WHISPER-95 acceptance scenarios. Pins the
// marshaling contract (CheckAccess fast-path inline, non-blocking post otherwise) and the graceful
// degradation: no presenter or a throwing presenter logs a warning and never lets an exception escape
// — surfacing one failure must not be able to cause another.

using AwesomeAssertions;
using Logic.AppManagement.Notifications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Logic.AppManagement.Tests;

public sealed class TrayUserNotifierTests
{
	private readonly TestUiDispatcher _dispatcher = new();
	private readonly List<(string Title, string Message)> _balloons = [];

	[Fact]
	public void Posts_through_the_dispatcher_when_off_the_ui_thread()
	{
		TrayUserNotifier notifier = new(_dispatcher, NullLogger<TrayUserNotifier>.Instance);
		notifier.AttachPresenter((title, message) => _balloons.Add((title, message)));
		_dispatcher.IsOnUiThread = false;

		notifier.NotifyError("title", "message");

		_dispatcher.PostCount.Should().Be(1);
		_balloons.Should().ContainSingle();
	}

	[Fact]
	public void Presents_inline_when_already_on_the_ui_thread()
	{
		TrayUserNotifier notifier = new(_dispatcher, NullLogger<TrayUserNotifier>.Instance);
		notifier.AttachPresenter((title, message) => _balloons.Add((title, message)));
		_dispatcher.IsOnUiThread = true;

		notifier.NotifyError("title", "message");

		_dispatcher.PostCount.Should().Be(0);
		_balloons.Should().ContainSingle();
	}

	[Fact]
	public void Logs_and_swallows_when_no_presenter_is_attached()
	{
		RecordingTestLogger logger = new();
		TrayUserNotifier notifier = new(_dispatcher, logger);

		Action act = () => notifier.NotifyError("title", "message");

		act.Should().NotThrow();
		logger.Warnings.Should().ContainSingle();
	}

	[Fact]
	public void Logs_and_swallows_a_presenter_failure()
	{
		RecordingTestLogger logger = new();
		TrayUserNotifier notifier = new(_dispatcher, logger);
		notifier.AttachPresenter((_, _) => throw new InvalidOperationException("suppressed"));

		Action act = () => notifier.NotifyError("title", "message");

		act.Should().NotThrow();
		logger.Warnings.Should().ContainSingle();
	}

	private sealed class RecordingTestLogger : ILogger<TrayUserNotifier>
	{
		public List<string> Warnings { get; } = [];

		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
			if (logLevel == LogLevel.Warning)
			{
				Warnings.Add(formatter(state, exception));
			}
		}
	}
}
