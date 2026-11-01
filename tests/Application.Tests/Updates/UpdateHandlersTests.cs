// Unit tests for the user-initiated update handlers, covering what the WHISPER-151 scenarios do not:
// cancellation propagates instead of being reported as a failure, and a download that fails part-way never
// stages anything or shuts the app down. Pure orchestration over substituted ports; no Velopack, no network.

using Application.Ports;
using Application.Updates;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Application.Tests.Updates;

public sealed class UpdateHandlersTests
{
	private readonly IUpdateSource _source = Substitute.For<IUpdateSource>();
	private readonly IAppLifetime _lifetime = Substitute.For<IAppLifetime>();

	public UpdateHandlersTests() => _source.IsInstalled.Returns(true);

	[Fact]
	public async Task A_cancelled_check_propagates()
	{
		_source.CheckForUpdatesAsync(Arg.Any<CancellationToken>())
			.Returns<ValueTask<AvailableUpdate?>>(_ => throw new OperationCanceledException());
		CheckForUpdateHandler handler = new(_source, NullLogger<CheckForUpdateHandler>.Instance);

		await Assert.ThrowsAsync<OperationCanceledException>(
			() => handler.Handle(new CheckForUpdateQuery(), CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task A_cancelled_install_propagates_without_staging_or_shutting_down()
	{
		_source.DownloadAsync(Arg.Any<AvailableUpdate>(), Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>())
			.Returns<ValueTask>(_ => throw new OperationCanceledException());
		InstallUpdateHandler handler = new(_source, _lifetime, NullLogger<InstallUpdateHandler>.Instance);

		await Assert.ThrowsAsync<OperationCanceledException>(
			() => handler.Handle(new InstallUpdateCommand("0.6.0"), CancellationToken.None).AsTask());

		_source.DidNotReceive().ApplyOnExitAndRestart(Arg.Any<AvailableUpdate>());
		_lifetime.DidNotReceive().RequestShutdown();
	}

	[Fact]
	public async Task Staging_failure_keeps_the_app_running()
	{
		_source.When(s => s.ApplyOnExitAndRestart(Arg.Any<AvailableUpdate>()))
			.Do(_ => throw new InvalidOperationException("Update 0.6.0 has not been downloaded."));
		InstallUpdateHandler handler = new(_source, _lifetime, NullLogger<InstallUpdateHandler>.Instance);

		UpdateInstallResult result = await handler.Handle(new InstallUpdateCommand("0.6.0"), CancellationToken.None);

		Assert.False(result.Restarting);
		Assert.Equal("Update 0.6.0 has not been downloaded.", result.Error);
		_lifetime.DidNotReceive().RequestShutdown();
	}
}
