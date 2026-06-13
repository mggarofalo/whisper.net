// Drives the first-run-setup scenarios. There is no separate onboarding flow any more — the
// launch decision is a single GetSetupStatusQuery over the REAL Mediator pipeline (the real model catalog
// from AddModelManagement), faking only the settings store and the model cache. So it proves the decision
// the App startup uses: a fresh install and a completed-but-missing-model both report not-configured (the
// settings window opens), a completed setup with a cached model reports configured (tray-only), and
// activating a model marks setup complete so the next launch needs no setup. The App.xaml wiring that calls
// IShellPresenter.ShowSettings when not configured is Presentation glue verified by smoke.

using Application.Models;
using Application.Ports;
using Application.Settings;
using AwesomeAssertions;
using Domain.Models;
using Domain.Settings;
using Mediator;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class SetupStatusDriver
{
	private readonly IMediator _mediator;
	private readonly IModelCache _cache;

	private AppSettings _persisted = AppSettings.Default;
	private SetupStatus? _status;

	public SetupStatusDriver(IMediator mediator, ISettingsStore store, IModelCache cache, IModelLifecycle lifecycle)
	{
		_mediator = mediator;
		_cache = cache;

		lifecycle.Status.Returns(ModelStatus.Unloaded);
		store.LoadAsync(Arg.Any<CancellationToken>()).Returns(_ => _persisted);
		store.When(s => s.SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>()))
			.Do(call => _persisted = call.Arg<AppSettings>());
	}

	public void SetupWasCompleted() => _persisted = new AppSettings(
		_persisted.ModelId, _persisted.Hotkey, _persisted.SilenceThresholdMs, _persisted.FillerWordRemovalEnabled,
		_persisted.CaptureDeviceId, _persisted.AuditLogEnabled, setupCompleted: true);

	public void ModelIsDownloaded(string id) =>
		_cache.IsCached(Arg.Is<WhisperModelCatalogEntry>(entry => entry.Id == id)).Returns(true);

	public void ModelIsNotDownloaded(string id) =>
		_cache.IsCached(Arg.Is<WhisperModelCatalogEntry>(entry => entry.Id == id)).Returns(false);

	public Task ActivateModel(string id) => _mediator.Send(new SwitchActiveModelCommand(id)).AsTask();

	public async Task CheckSetup() => _status = await _mediator.Send(new GetSetupStatusQuery());

	public void AssertConfigured() => _status!.IsConfigured.Should().BeTrue("the app should go straight to the tray");

	public void AssertNotConfigured() => _status!.IsConfigured.Should().BeFalse("the settings window should open for setup");
}
