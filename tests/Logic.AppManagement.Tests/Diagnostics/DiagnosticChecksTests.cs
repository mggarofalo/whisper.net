// Unit tests for the individual self-diagnostic checks. Each check is exercised over
// substituted ports for both its healthy and unavailable paths, asserting the status AND that the detail
// names the concrete thing a user needs (the device, the model path, the chord, the backend reason). The
// BDD feature proves the checks compose through the real Mediator pipeline; these pin each verdict.

using Application.Diagnostics;
using Application.Ports;
using AwesomeAssertions;
using Domain.Audio;
using Domain.Models;
using Domain.Settings;
using Logic.AppManagement.Diagnostics;
using NSubstitute;
using Xunit;

namespace Logic.AppManagement.Tests.Diagnostics;

public sealed class DiagnosticChecksTests
{
	private static readonly WhisperModelCatalogEntry BaseEn =
		new("base.en", "Base (English)", "f16", "ggml-base.en.bin", 142 * 1024 * 1024);

	// --- Audio ---

	[Fact]
	public async Task Audio_passes_and_names_the_default_when_a_device_is_present()
	{
		IAudioDeviceEnumerator devices = Substitute.For<IAudioDeviceEnumerator>();
		devices.GetCaptureDevices().Returns([new AudioDevice("mic-1", "Microphone")]);
		devices.GetSystemDefaultId().Returns("mic-1");

		DiagnosticResult result = await new AudioCaptureCheck(devices).RunAsync(CancellationToken.None);

		result.Status.Should().Be(DiagnosticStatus.Pass);
		result.Detail.Should().Contain("Microphone");
	}

	[Fact]
	public async Task Audio_fails_when_no_capture_device_is_available()
	{
		IAudioDeviceEnumerator devices = Substitute.For<IAudioDeviceEnumerator>();
		devices.GetCaptureDevices().Returns([]);

		DiagnosticResult result = await new AudioCaptureCheck(devices).RunAsync(CancellationToken.None);

		result.Status.Should().Be(DiagnosticStatus.Fail);
	}

	// --- Model ---

	private static (ISettingsStore store, IModelCatalog catalog, IModelCache cache) ModelPorts(string modelId)
	{
		ISettingsStore store = Substitute.For<ISettingsStore>();
		AppSettings settings = new(modelId, HotkeyBinding.Parse("Ctrl+Win"), 500, true);
		store.LoadAsync(Arg.Any<CancellationToken>()).Returns(settings);

		IModelCatalog catalog = Substitute.For<IModelCatalog>();
		IModelCache cache = Substitute.For<IModelCache>();
		cache.GetCachedPath(Arg.Any<WhisperModelCatalogEntry>()).Returns(@"C:\models\ggml-base.en.bin");
		return (store, catalog, cache);
	}

	[Fact]
	public async Task Model_passes_and_names_the_path_when_cached()
	{
		(ISettingsStore store, IModelCatalog catalog, IModelCache cache) = ModelPorts("base.en");
		catalog.Find("base.en").Returns(BaseEn);
		cache.IsCached(BaseEn).Returns(true);

		DiagnosticResult result = await new ModelCacheCheck(store, catalog, cache).RunAsync(CancellationToken.None);

		result.Status.Should().Be(DiagnosticStatus.Pass);
		result.Detail.Should().Contain(@"C:\models\ggml-base.en.bin");
	}

	[Fact]
	public async Task Model_fails_when_not_downloaded()
	{
		(ISettingsStore store, IModelCatalog catalog, IModelCache cache) = ModelPorts("base.en");
		catalog.Find("base.en").Returns(BaseEn);
		cache.IsCached(BaseEn).Returns(false);

		DiagnosticResult result = await new ModelCacheCheck(store, catalog, cache).RunAsync(CancellationToken.None);

		result.Status.Should().Be(DiagnosticStatus.Fail);
	}

	[Fact]
	public async Task Model_fails_when_the_configured_id_is_unknown()
	{
		(ISettingsStore store, IModelCatalog catalog, IModelCache cache) = ModelPorts("not-a-model");
		catalog.Find("not-a-model").Returns((WhisperModelCatalogEntry?)null);

		DiagnosticResult result = await new ModelCacheCheck(store, catalog, cache).RunAsync(CancellationToken.None);

		result.Status.Should().Be(DiagnosticStatus.Fail);
		result.Detail.Should().Contain("not-a-model");
	}

	// --- Hotkey ---

	[Fact]
	public async Task Hotkey_passes_and_names_the_chord_when_permitted()
	{
		ISettingsStore store = Substitute.For<ISettingsStore>();
		store.LoadAsync(Arg.Any<CancellationToken>()).Returns(AppSettings.Default);
		IPermissionProbe permissions = Substitute.For<IPermissionProbe>();
		permissions.HasRequiredInputPermissions().Returns(true);

		DiagnosticResult result = await new HotkeyCheck(store, permissions).RunAsync(CancellationToken.None);

		result.Status.Should().Be(DiagnosticStatus.Pass);
		result.Detail.Should().Contain(AppSettings.Default.Hotkey.Chord);
	}

	[Fact]
	public async Task Hotkey_fails_when_the_input_permission_is_denied()
	{
		ISettingsStore store = Substitute.For<ISettingsStore>();
		store.LoadAsync(Arg.Any<CancellationToken>()).Returns(AppSettings.Default);
		IPermissionProbe permissions = Substitute.For<IPermissionProbe>();
		permissions.HasRequiredInputPermissions().Returns(false);

		DiagnosticResult result = await new HotkeyCheck(store, permissions).RunAsync(CancellationToken.None);

		result.Status.Should().Be(DiagnosticStatus.Fail);
	}

	// --- GPU ---

	[Fact]
	public async Task Gpu_passes_when_vulkan_is_selected()
	{
		IBackendSelector selector = Substitute.For<IBackendSelector>();
		selector.SelectBackendAsync(Arg.Any<CancellationToken>())
			.Returns(new BackendSelection(ComputeBackend.Vulkan, "A usable Vulkan runtime is available."));

		DiagnosticResult result = await new GpuCheck(selector).RunAsync(CancellationToken.None);

		result.Status.Should().Be(DiagnosticStatus.Pass);
	}

	[Fact]
	public async Task Gpu_warns_rather_than_fails_when_falling_back_to_cpu()
	{
		IBackendSelector selector = Substitute.For<IBackendSelector>();
		selector.SelectBackendAsync(Arg.Any<CancellationToken>())
			.Returns(new BackendSelection(ComputeBackend.Cpu, "No usable Vulkan runtime is available; using the CPU backend."));

		DiagnosticResult result = await new GpuCheck(selector).RunAsync(CancellationToken.None);

		result.Status.Should().Be(DiagnosticStatus.Warn);
		result.Detail.Should().Contain("CPU");
	}

	// --- Startup (launch at login) ---

	private const string ThisInstall = "\"C:\\Users\\u\\AppData\\Local\\Whisper.Net\\Presentation.exe\"";
	private const string OtherInstall = "\"D:\\Old\\Whisper.Net\\current\\Presentation.exe\"";

	private static IStartupRegistration Registration(string? registered, bool targetExists)
	{
		IStartupRegistration registration = Substitute.For<IStartupRegistration>();
		registration.ExpectedCommand.Returns(ThisInstall);
		registration.RegisteredCommand.Returns(registered);
		registration.IsEnabled().Returns(registered is not null);
		registration.RegisteredTargetExists.Returns(targetExists);
		return registration;
	}

	[Fact]
	public async Task Startup_passes_and_names_the_command_when_registered_for_this_install()
	{
		DiagnosticResult result = await new StartupRegistrationCheck(Registration(ThisInstall, targetExists: true))
			.RunAsync(CancellationToken.None);

		result.Status.Should().Be(DiagnosticStatus.Pass);
		result.Detail.Should().Contain("Presentation.exe");
	}

	// The exact failure a user hits after reinstalling elsewhere: the toggle still reads as on, and nothing
	// launches at login. The report must say so rather than call the system healthy.
	[Fact]
	public async Task Startup_fails_when_the_registration_points_at_a_removed_install()
	{
		DiagnosticResult result = await new StartupRegistrationCheck(Registration(OtherInstall, targetExists: false))
			.RunAsync(CancellationToken.None);

		result.Status.Should().Be(DiagnosticStatus.Fail);
		result.Detail.Should().Contain("no longer exists");
	}

	[Fact]
	public async Task Startup_warns_when_the_registration_points_at_another_install()
	{
		DiagnosticResult result = await new StartupRegistrationCheck(Registration(OtherInstall, targetExists: true))
			.RunAsync(CancellationToken.None);

		result.Status.Should().Be(DiagnosticStatus.Warn);
	}

	// Choosing not to start at login is not a defect, so it must not put a standing warning in the report.
	[Fact]
	public async Task Startup_passes_when_launch_at_login_is_switched_off()
	{
		DiagnosticResult result = await new StartupRegistrationCheck(Registration(registered: null, targetExists: false))
			.RunAsync(CancellationToken.None);

		result.Status.Should().Be(DiagnosticStatus.Pass);
	}
}
