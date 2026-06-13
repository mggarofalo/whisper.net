// Unit tests for GetSetupStatusHandler: the launch decision that drives whether the settings
// window opens for first-run setup. The app is configured only when setup is completed AND the selected
// model is present in the local cache, so a fresh install, a completed setup whose model file is gone, and
// an unknown model id all report not-configured (and the launch flow opens settings).

using Application.Ports;
using Application.Settings;
using Domain.Models;
using Domain.Settings;
using NSubstitute;
using Xunit;

namespace Application.Tests.Settings;

public sealed class GetSetupStatusHandlerTests
{
	private static readonly WhisperModelCatalogEntry Base =
		new("base.en", "Base (EN)", "q5", "base.en.bin", 100);

	private readonly ISettingsStore _store = Substitute.For<ISettingsStore>();
	private readonly IModelCatalog _catalog = Substitute.For<IModelCatalog>();
	private readonly IModelCache _cache = Substitute.For<IModelCache>();

	private static AppSettings Settings(bool setupCompleted) =>
		new("base.en", HotkeyBinding.Parse("Ctrl+Win"), 500, fillerWordRemovalEnabled: true, setupCompleted: setupCompleted);

	private GetSetupStatusHandler NewHandler() => new(_store, _catalog, _cache);

	[Fact]
	public async Task Configured_when_setup_completed_and_the_model_is_cached()
	{
		_store.LoadAsync(Arg.Any<CancellationToken>()).Returns(Settings(setupCompleted: true));
		_catalog.Find("base.en").Returns(Base);
		_cache.IsCached(Base).Returns(true);

		SetupStatus status = await NewHandler().Handle(new GetSetupStatusQuery(), CancellationToken.None);

		Assert.True(status.IsConfigured);
	}

	[Fact]
	public async Task Not_configured_when_setup_is_incomplete()
	{
		_store.LoadAsync(Arg.Any<CancellationToken>()).Returns(Settings(setupCompleted: false));
		_catalog.Find("base.en").Returns(Base);
		_cache.IsCached(Base).Returns(true);

		SetupStatus status = await NewHandler().Handle(new GetSetupStatusQuery(), CancellationToken.None);

		Assert.False(status.IsConfigured);
	}

	[Fact]
	public async Task Not_configured_when_the_selected_model_is_no_longer_cached()
	{
		_store.LoadAsync(Arg.Any<CancellationToken>()).Returns(Settings(setupCompleted: true));
		_catalog.Find("base.en").Returns(Base);
		_cache.IsCached(Base).Returns(false);

		SetupStatus status = await NewHandler().Handle(new GetSetupStatusQuery(), CancellationToken.None);

		Assert.False(status.IsConfigured, "a completed setup whose model is gone has no active model");
	}

	[Fact]
	public async Task Not_configured_when_the_model_id_is_unknown()
	{
		_store.LoadAsync(Arg.Any<CancellationToken>()).Returns(Settings(setupCompleted: true));
		_catalog.Find("base.en").Returns((WhisperModelCatalogEntry?)null);

		SetupStatus status = await NewHandler().Handle(new GetSetupStatusQuery(), CancellationToken.None);

		Assert.False(status.IsConfigured);
	}
}
