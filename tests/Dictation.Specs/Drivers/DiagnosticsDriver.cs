// Drives the self-diagnostics scenarios. It owns HOW the doctor run is exercised so the
// steps stay one-liners: it configures the faked Infrastructure ports each check reads (the device
// enumerator, the settings store, the model cache, the permission probe, and the raw GPU probe behind
// the real backend selector), then runs the REAL diagnostics through the Mediator pipeline
// (RunDiagnosticsQuery -> the Application handler -> the Logic checks) and asserts on the structured
// DiagnosticReport. Only the device-facing seams are faked; the checks, the aggregation, and the
// GPU contact point run for real — so the behavior is validated, not mocked.

using Application.Diagnostics;
using Application.Ports;
using AwesomeAssertions;
using Dictation.Specs.Support;
using Domain.Audio;
using Domain.Settings;
using Mediator;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class DiagnosticsDriver
{
	// The subsystems the doctor reports on, in report order. Used to assert every check still produced a
	// result even when one is unavailable.
	private static readonly string[] ExpectedChecks = ["Audio", "Model", "Whisper", "Hotkey", "GPU"];

	private readonly IMediator _mediator;
	private readonly FakeAudioDeviceEnumerator _audioDevices;
	private readonly IModelCache _modelCache;
	private readonly IWhisperRuntimeProbe _whisperProbe;
	private readonly IPermissionProbe _permissions;
	private readonly IGpuProbe _gpuProbe;

	private DiagnosticReport? _report;

	public DiagnosticsDriver(
		IMediator mediator,
		FakeAudioDeviceEnumerator audioDevices,
		ISettingsStore settings,
		IModelCache modelCache,
		IWhisperRuntimeProbe whisperProbe,
		IPermissionProbe permissions,
		IGpuProbe gpuProbe)
	{
		_mediator = mediator;
		_audioDevices = audioDevices;
		_modelCache = modelCache;
		_whisperProbe = whisperProbe;
		_permissions = permissions;
		_gpuProbe = gpuProbe;

		// The configured model + hotkey come from the store; default settings select the cataloged
		// "base.en" model and a valid chord, so the model and hotkey checks have something real to resolve.
		settings.LoadAsync(Arg.Any<CancellationToken>()).Returns(AppSettings.Default);

		// The cache port always reports a path, so a Pass names where the model lives and a Fail names where
		// it was expected.
		_modelCache.GetCachedPath(Arg.Any<Domain.Models.WhisperModelCatalogEntry>()).Returns(@"C:\models\ggml-base.en.bin");

		// The Whisper native runtime loads by default so the other checks have a healthy peer; the
		// "Whisper unavailable" given overrides it with the packaging-failure verdict.
		_whisperProbe.Probe().Returns(new WhisperRuntimeStatus(true, "Whisper native runtime loaded."));
	}

	// --- given (each makes one subsystem healthy; Healthy() makes them all so) ---

	public void CaptureDeviceAvailable() =>
		_audioDevices.Configure([new AudioDevice("mic-1", "Microphone")], "mic-1");

	public void ModelDownloaded() =>
		_modelCache.IsCached(Arg.Any<Domain.Models.WhisperModelCatalogEntry>()).Returns(true);

	public void HotkeyPermissionGranted() =>
		_permissions.HasRequiredInputPermissions().Returns(true);

	public void VulkanAvailable() =>
		_gpuProbe.IsGpuRuntimeAvailableAsync(Arg.Any<CancellationToken>()).Returns(true);

	public void Healthy()
	{
		CaptureDeviceAvailable();
		ModelDownloaded();
		HotkeyPermissionGranted();
		VulkanAvailable();
	}

	// --- given (break exactly one subsystem) ---

	public void SubsystemUnavailable(string subsystem)
	{
		switch (subsystem)
		{
			case "Audio":
				_audioDevices.Configure([], defaultId: null);
				break;
			case "Model":
				_modelCache.IsCached(Arg.Any<Domain.Models.WhisperModelCatalogEntry>()).Returns(false);
				break;
			case "Whisper":
				_whisperProbe.Probe().Returns(new WhisperRuntimeStatus(
					false, "Whisper native runtime could not be loaded: Native Library not found in default paths."));
				break;
			case "Hotkey":
				_permissions.HasRequiredInputPermissions().Returns(false);
				break;
			case "GPU":
				_gpuProbe.IsGpuRuntimeAvailableAsync(Arg.Any<CancellationToken>()).Returns(false);
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(subsystem), subsystem, "Unknown diagnostic subsystem.");
		}
	}

	// --- when ---

	public async Task RunDiagnostics() => _report = await _mediator.Send(new RunDiagnosticsQuery());

	// --- then ---

	public void AssertEveryCheckPasses() =>
		Report().Results.Should().OnlyContain(result => result.Status == DiagnosticStatus.Pass);

	public void AssertCheckFails(string name) =>
		Find(name).Status.Should().Be(DiagnosticStatus.Fail);

	public void AssertCheckDoesNotFail(string name) =>
		Find(name).Status.Should().NotBe(DiagnosticStatus.Fail);

	public void AssertEverySubsystemProducedAResult() =>
		Report().Results.Select(result => result.Name).Should().BeEquivalentTo(ExpectedChecks);

	public void AssertCheckDetailContains(string name, string fragment) =>
		Find(name).Detail.Should().ContainEquivalentOf(fragment);

	public void AssertEveryResultIsStructured()
	{
		Report().Results.Should().NotBeEmpty();
		Report().Results.Should().OnlyContain(result =>
			!string.IsNullOrWhiteSpace(result.Name) && !string.IsNullOrWhiteSpace(result.Detail));
	}

	private DiagnosticResult Find(string name) =>
		Report().Results.Should().ContainSingle(result => result.Name == name).Subject;

	private DiagnosticReport Report() =>
		_report ?? throw new InvalidOperationException("Run the diagnostics before asserting.");
}
