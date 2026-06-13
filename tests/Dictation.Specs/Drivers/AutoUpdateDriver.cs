// Drives the signed auto-update scenarios. Like the GPU-backend driver, it constructs the
// REAL AutoUpdateService over a faked IUpdateSource and a scenario-controlled options + recording logger,
// so the actual policy runs — check, download/apply, opt-in gating, and graceful degradation on failure —
// with no Velopack and no network. It also inspects the packaging script to assert the installer is
// configured to be code-signed when a certificate is supplied (the signing plumbing); a real signed build
// and an end-to-end update on a test machine are environmental and tracked as follow-ups.

using Application.Configuration;
using Application.Ports;
using Application.Updates;
using AwesomeAssertions;
using Dictation.Specs.Support;
using Logic.AppManagement.Updates;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class AutoUpdateDriver
{
	private static readonly string RepositoryRoot = FindRepositoryRoot();

	private readonly IUpdateSource _source;
	private readonly AutoUpdateOptions _options = new();
	private readonly RecordingLogger<AutoUpdateService> _logger = new();
	private readonly AutoUpdateService _service;
	private UpdateOutcome _outcome;

	public AutoUpdateDriver(IUpdateSource source)
	{
		_source = source;
		_service = new AutoUpdateService(source, Options.Create(_options), _logger);
	}

	// --- given ---

	public void AutomaticUpdatesEnabled() => _options.Enabled = true;

	public void AutomaticUpdatesDisabled() => _options.Enabled = false;

	public void NewerReleaseAvailable(string version) =>
		_source.CheckForUpdatesAsync(Arg.Any<CancellationToken>()).Returns(new AvailableUpdate(version));

	public void AlreadyUpToDate() =>
		_source.CheckForUpdatesAsync(Arg.Any<CancellationToken>()).Returns((AvailableUpdate?)null);

	public void ChannelUnreachable() =>
		_source.CheckForUpdatesAsync(Arg.Any<CancellationToken>())
			.Returns<ValueTask<AvailableUpdate?>>(_ => throw new HttpRequestException("The update channel is unreachable."));

	// --- when ---

	public async Task CheckForUpdates() => _outcome = await _service.UpdateIfAvailableAsync(CancellationToken.None);

	// --- then ---

	public async Task AssertUpdateDownloadedAndStaged(string version)
	{
		_outcome.Should().Be(UpdateOutcome.Updated);
		await _source.Received(1).ApplyUpdateAsync(Arg.Is<AvailableUpdate>(u => u.Version == version), Arg.Any<CancellationToken>());
	}

	public void AssertContinuesOnCurrentVersion() =>
		_outcome.Should().BeOneOf(UpdateOutcome.Failed, UpdateOutcome.UpToDate, UpdateOutcome.Disabled);

	public void AssertFailureLogged() =>
		_logger.Entries.Should().Contain(entry => entry.Level == LogLevel.Error);

	public async Task AssertNoUpdateApplied() =>
		await _source.DidNotReceive().ApplyUpdateAsync(Arg.Any<AvailableUpdate>(), Arg.Any<CancellationToken>());

	public async Task AssertNoUpdateCheckPerformed()
	{
		_outcome.Should().Be(UpdateOutcome.Disabled);
		await _source.DidNotReceive().CheckForUpdatesAsync(Arg.Any<CancellationToken>());
	}

	// --- signing plumbing (AC2) ---

	public void AssertInstallerSignedWhenCertificateProvided()
	{
		string script = File.ReadAllText(Path.Combine(RepositoryRoot, "build", "pack.ps1"));
		// The packaging signs the build when a signing certificate is supplied via the environment, and the
		// certificate/password come from the environment (a secret), never a committed value.
		script.Should().Contain("VELOPACK_SIGN_CERTIFICATE");
		script.Should().Contain("--signParams");
	}

	private static string FindRepositoryRoot()
	{
		DirectoryInfo? directory = new(AppContext.BaseDirectory);
		while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Whisper.slnx")))
		{
			directory = directory.Parent;
		}

		return directory?.FullName
			?? throw new InvalidOperationException("Could not locate the repository root (Whisper.slnx).");
	}
}
