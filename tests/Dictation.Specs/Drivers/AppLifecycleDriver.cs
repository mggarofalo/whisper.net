// Drives the host-bootstrap scenarios. It owns HOW a real Generic Host is composed,
// launched, and shut down so the step definitions stay one-liners. The host is built from the SAME
// per-layer registration extensions the production WPF app uses; only the Infrastructure hotkey seam
// is faked, so the REAL hosted services (the hotkey listener) and the host's start/stop lifecycle are
// exercised for real with no OS hook and no window.

using Application.DependencyInjection;
using Application.Ports;
using AwesomeAssertions;
using Dictation.Specs.Support;
using Domain.Settings;
using Infrastructure.Hotkeys;
using Logic.AppManagement.DependencyInjection;
using Logic.AudioManagement.DependencyInjection;
using Logic.GpuContactPoint.DependencyInjection;
using Logic.ModelManagement.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class AppLifecycleDriver : IDisposable
{
	private readonly LifecycleProbe _probe = new();
	private FakeGlobalKeyHook? _hook;
	private IHost? _host;
	private bool _stopped;

	public void ComposeHost()
	{
		HostApplicationBuilder builder = Host.CreateApplicationBuilder();
		builder.Services.AddLogging();

		// The same per-layer registration extensions the production host composes.
		builder.Services.AddApplication();
		builder.Services.AddAppManagement();
		builder.Services.AddAudioManagement();
		builder.Services.AddModelManagement();
		builder.Services.AddGpuContactPoint();

		// Fake ONLY the Infrastructure hotkey seam, then compose the REAL EventLoopHotkeyListener over
		// it (exactly as the hotkey specs do), so the hosted service starts a real listener with
		// no OS hook installed.
		_hook = new FakeGlobalKeyHook();
		builder.Services.AddSingleton<IGlobalKeyHook>(_hook);
		builder.Services.AddSingleton<IHotkeyListener, EventLoopHotkeyListener>();

		// The host-owned background components also include settings persistence; supply a
		// stub store so the host composes it without touching disk. This driver asserts the hosted-service
		// lifecycle, not settings, so defaults are fine.
		ISettingsStore settingsStore = Substitute.For<ISettingsStore>();
		settingsStore.LoadAsync(Arg.Any<CancellationToken>()).Returns(AppSettings.Default);
		builder.Services.AddSingleton(settingsStore);

		// The dictation orchestrator activated by the host needs the audio capture port and
		// the audio-feedback port; this driver asserts the hosted-service lifecycle, not
		// capture or feedback, so substitutes that open no device are enough for the host to start.
		builder.Services.AddSingleton(Substitute.For<IAudioSource>());
		builder.Services.AddSingleton(Substitute.For<IAudioFeedback>());

		// The model warm-up hosted service preloads the dictation model via ITranscriber; a
		// substitute that no-ops keeps the host composing without a real model or native library.
		builder.Services.AddSingleton(Substitute.For<ITranscriber>());

		// The host also runs the auto-update check hosted service; supply a stub source so the
		// host composes without any network. This driver asserts the hosted-service lifecycle, not updates.
		builder.Services.AddSingleton(Substitute.For<IUpdateSource>());

		// The launch-at-login upkeep hosted service reads the startup registration on start; a substitute
		// keeps this host away from the machine's real Run key. A substitute reports "not registered", so
		// the service logs and does nothing.
		builder.Services.AddSingleton(Substitute.For<IStartupRegistration>());

		// The host-owned background components under test.
		builder.Services.AddAppManagementHostedServices();

		// A probe hosted service so the scenarios can assert the host drives an arbitrary IHostedService
		// through its full lifecycle, not just the one production component.
		builder.Services.AddSingleton(_probe);
		builder.Services.AddHostedService(sp => new ProbeHostedService(sp.GetRequiredService<LifecycleProbe>()));

		_host = builder.Build();
	}

	public Task LaunchAsync() => RunningHost.StartAsync();

	public async Task RequestShutdownAsync()
	{
		await RunningHost.StopAsync();
		_stopped = true;
	}

	public void AssertEveryHostedServiceStarted() =>
		_probe.Started.Should().BeTrue("the host should have started every registered hosted service");

	public void AssertHotkeyListenerObserving() =>
		SpinUntil(() => _hook!.IsRunning).Should().BeTrue("the hosted service should have started the global hotkey listener");

	public void AssertRunningWithNoWindowShown()
	{
		IHostApplicationLifetime lifetime = RunningHost.Services.GetRequiredService<IHostApplicationLifetime>();
		SpinUntil(() => lifetime.ApplicationStarted.IsCancellationRequested)
			.Should().BeTrue("the host should have reached the Started state");
		lifetime.ApplicationStopped.IsCancellationRequested
			.Should().BeFalse("the app should run tray-resident, not show then close a startup window");
	}

	public void AssertEveryHostedServiceStoppedBeforeExit()
	{
		// RequestShutdownAsync awaited StopAsync but has NOT disposed the host, so observing Stopped here
		// proves every hosted service was stopped before the host (and process) exits.
		_stopped.Should().BeTrue();
		_probe.Stopped.Should().BeTrue("the host should have stopped every hosted service before exiting");
	}

	public void AssertHotkeyListenerStopped() =>
		SpinUntil(() => !_hook!.IsRunning).Should().BeTrue("the listener should stop observing on graceful shutdown");

	private IHost RunningHost => _host ?? throw new InvalidOperationException("ComposeHost was not called.");

	private static bool SpinUntil(Func<bool> condition) => SpinWait.SpinUntil(condition, TimeSpan.FromSeconds(2));

	// Reqnroll disposes the scenario container synchronously, so the driver is IDisposable (not
	// IAsyncDisposable): block on StopAsync to stop any still-running hosted services, then dispose.
	public void Dispose()
	{
		if (_host is null)
		{
			return;
		}

		if (!_stopped)
		{
			_host.StopAsync().GetAwaiter().GetResult();
		}

		_host.Dispose();
	}
}
