// Unit tests for the auto-update policy: the opt-in gate (no check when disabled, so no
// egress), the happy path (a newer release is downloaded and staged), the up-to-date path, and graceful
// degradation (an unreachable channel is logged and swallowed, never thrown). Cancellation propagates.
// Uses a substituted IUpdateSource and a recording logger; no Velopack, no network.

using Application.Configuration;
using Application.Ports;
using Application.Updates;
using AwesomeAssertions;
using Logic.AppManagement.Updates;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Logic.AppManagement.Tests.Updates;

public sealed class AutoUpdateServiceTests
{
	private readonly IUpdateSource _source = Substitute.For<IUpdateSource>();
	private readonly RecordingLogger _logger = new();

	private AutoUpdateService NewService(bool enabled) =>
		new(_source, Options.Create(new AutoUpdateOptions { Enabled = enabled }), _logger);

	[Fact]
	public async Task Does_not_check_when_disabled()
	{
		UpdateOutcome outcome = await NewService(enabled: false).UpdateIfAvailableAsync(CancellationToken.None);

		outcome.Should().Be(UpdateOutcome.Disabled);
		await _source.DidNotReceive().CheckForUpdatesAsync(Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Downloads_and_stages_a_newer_release()
	{
		_source.CheckForUpdatesAsync(Arg.Any<CancellationToken>()).Returns(new AvailableUpdate("0.2.0"));

		UpdateOutcome outcome = await NewService(enabled: true).UpdateIfAvailableAsync(CancellationToken.None);

		outcome.Should().Be(UpdateOutcome.Updated);
		await _source.Received(1).ApplyUpdateAsync(Arg.Is<AvailableUpdate>(u => u.Version == "0.2.0"), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Reports_up_to_date_and_applies_nothing()
	{
		_source.CheckForUpdatesAsync(Arg.Any<CancellationToken>()).Returns((AvailableUpdate?)null);

		UpdateOutcome outcome = await NewService(enabled: true).UpdateIfAvailableAsync(CancellationToken.None);

		outcome.Should().Be(UpdateOutcome.UpToDate);
		await _source.DidNotReceive().ApplyUpdateAsync(Arg.Any<AvailableUpdate>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Degrades_gracefully_and_logs_when_the_channel_is_unreachable()
	{
		_source.CheckForUpdatesAsync(Arg.Any<CancellationToken>())
			.Returns<ValueTask<AvailableUpdate?>>(_ => throw new HttpRequestException("unreachable"));

		UpdateOutcome outcome = await NewService(enabled: true).UpdateIfAvailableAsync(CancellationToken.None);

		outcome.Should().Be(UpdateOutcome.Failed);
		_logger.Entries.Should().Contain(entry => entry.Level == LogLevel.Error);
	}

	[Fact]
	public async Task Propagates_cancellation()
	{
		using CancellationTokenSource cts = new();
		cts.Cancel();
		_source.CheckForUpdatesAsync(Arg.Any<CancellationToken>())
			.Returns<ValueTask<AvailableUpdate?>>(_ => throw new OperationCanceledException());

		await Assert.ThrowsAsync<OperationCanceledException>(async () =>
			await NewService(enabled: true).UpdateIfAvailableAsync(cts.Token));
	}

	// A minimal recording ILogger<AutoUpdateService> so the test can assert the failure was logged.
	private sealed class RecordingLogger : ILogger<AutoUpdateService>
	{
		public List<(LogLevel Level, string Message)> Entries { get; } = [];

		public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
			Entries.Add((logLevel, formatter(state, exception)));

		private sealed class NullScope : IDisposable
		{
			public static readonly NullScope Instance = new();

			public void Dispose()
			{
			}
		}
	}
}
