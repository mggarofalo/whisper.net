// Drives the version-and-updates scenarios (WHISPER-151) over the REAL GeneralViewModel and Mediator
// pipeline: GetAppVersion / CheckForUpdate / InstallUpdate run through the real handlers, the automatic
// opt-in persists through the real UpdateSettings path into a round-tripping store, and the startup check
// runs the real AutoUpdateService policy against that store. Only the ports are faked: the update source
// (Velopack + GitHub), the build info (the MinVer attribute), and the app lifetime (graceful shutdown) — so
// "no network" is observed as "the update source was never asked".

using Application.Configuration;
using Application.Ports;
using Application.Updates;
using AwesomeAssertions;
using Domain.Settings;
using Logic.AppManagement.Shell;
using Logic.AppManagement.Updates;
using Mediator;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class VersionAndUpdatesDriver
{
	private readonly IUpdateSource _source;
	private readonly IBuildInfo _buildInfo;
	private readonly IAppLifetime _lifetime;
	private readonly ISettingsStore _store;
	private readonly GeneralViewModel _viewModel;

	private AppSettings _persisted = AppSettings.Default;

	public VersionAndUpdatesDriver(
		IMediator mediator,
		ISettingsStore store,
		IUpdateSource source,
		IBuildInfo buildInfo,
		IAppLifetime lifetime)
	{
		_store = store;
		_source = source;
		_buildInfo = buildInfo;
		_lifetime = lifetime;

		// Round-trip the store so a persisted opt-in is what the startup check reads back.
		store.LoadAsync(Arg.Any<CancellationToken>()).Returns(_ => _persisted);
		store.When(s => s.SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>()))
			.Do(call => _persisted = call.Arg<AppSettings>());

		_viewModel = new GeneralViewModel(mediator);
	}

	// --- given ---

	public void RunningBuildIs(string informationalVersion) =>
		_buildInfo.InformationalVersion.Returns(informationalVersion);

	public void InstalledBuild() => _source.IsInstalled.Returns(true);

	public void NotInstalledBuild() => _source.IsInstalled.Returns(false);

	public void ReleasePublished(string version) =>
		_source.CheckForUpdatesAsync(Arg.Any<CancellationToken>()).Returns(new AvailableUpdate(version));

	public void NoNewerRelease() =>
		_source.CheckForUpdatesAsync(Arg.Any<CancellationToken>()).Returns((AvailableUpdate?)null);

	public void FeedUnreachable() =>
		_source.CheckForUpdatesAsync(Arg.Any<CancellationToken>())
			.Returns<ValueTask<AvailableUpdate?>>(_ => throw new HttpRequestException("the release feed is unreachable"));

	public void DownloadFails() =>
		_source.DownloadAsync(Arg.Any<AvailableUpdate>(), Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>())
			.Returns<ValueTask>(_ => throw new IOException("the download was interrupted"));

	// --- when ---

	public Task OpenSection() => _viewModel.LoadCommand.ExecuteAsync(null);

	public Task CheckForUpdates() => _viewModel.CheckForUpdatesCommand.ExecuteAsync(null);

	public Task DownloadAndRestart() => _viewModel.InstallUpdateCommand.ExecuteAsync(null);

	public void TurnOnAutomaticChecks() => _viewModel.AutomaticUpdateChecks = true;

	// --- then ---

	public void AssertVersionShown(string expected) => _viewModel.AppVersion.Should().Be(expected);

	public async Task AssertNoUpdateCheckMade()
	{
		await _source.DidNotReceive().CheckForUpdatesAsync(Arg.Any<CancellationToken>());
		await _source.DidNotReceive().DownloadAsync(Arg.Any<AvailableUpdate>(), Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>());
		await _source.DidNotReceive().ApplyUpdateAsync(Arg.Any<AvailableUpdate>(), Arg.Any<CancellationToken>());
	}

	public void AssertStatus(string expected) => _viewModel.UpdateStatus.Should().Be(expected);

	public void AssertCanDownloadAndRestart()
	{
		_viewModel.IsUpdateAvailable.Should().BeTrue();
		_viewModel.InstallUpdateCommand.CanExecute(null).Should().BeTrue();
	}

	public void AssertCannotDownloadAndRestart()
	{
		_viewModel.IsUpdateAvailable.Should().BeFalse();
		_viewModel.InstallUpdateCommand.CanExecute(null).Should().BeFalse();
	}

	public async Task AssertDownloadedAndStagedForRestart(string version)
	{
		await _source.Received(1).DownloadAsync(
			Arg.Is<AvailableUpdate>(u => u.Version == version), Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>());
		_source.Received(1).ApplyOnExitAndRestart(Arg.Is<AvailableUpdate>(u => u.Version == version));
	}

	public void AssertShutdownRequested() => _lifetime.Received(1).RequestShutdown();

	public void AssertKeepsRunning()
	{
		_lifetime.DidNotReceive().RequestShutdown();
		_source.DidNotReceive().ApplyOnExitAndRestart(Arg.Any<AvailableUpdate>());
	}

	public void AssertAutomaticChecksOff()
	{
		_viewModel.AutomaticUpdateChecks.Should().BeFalse("automatic update checks are opt-in");
		_persisted.AutomaticUpdateChecksEnabled.Should().BeFalse();
	}

	public void AssertAutomaticChecksPersistedOn() =>
		_persisted.AutomaticUpdateChecksEnabled.Should().BeTrue("the opt-in is saved");

	public async Task AssertNoStartupCheck()
	{
		UpdateOutcome outcome = await RunStartupCheck();
		outcome.Should().Be(UpdateOutcome.Disabled);
		await _source.DidNotReceive().CheckForUpdatesAsync(Arg.Any<CancellationToken>());
	}

	public async Task AssertStartupCheckStages(string version)
	{
		UpdateOutcome outcome = await RunStartupCheck();
		outcome.Should().Be(UpdateOutcome.Updated);
		await _source.Received(1).ApplyUpdateAsync(Arg.Is<AvailableUpdate>(u => u.Version == version), Arg.Any<CancellationToken>());
	}

	// The real startup policy, reading the opt-in from the same store the section persisted to. The
	// configuration switch stays at its default (off), so only the user's setting can turn the check on.
	private ValueTask<UpdateOutcome> RunStartupCheck() =>
		new AutoUpdateService(_source, _store, Options.Create(new AutoUpdateOptions()), NullLogger<AutoUpdateService>.Instance)
			.UpdateIfAvailableAsync(CancellationToken.None);
}
