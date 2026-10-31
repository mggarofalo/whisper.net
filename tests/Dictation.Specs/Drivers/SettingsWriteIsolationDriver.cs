// Drives the settings-write isolation scenarios (WHISPER-142) over the REAL section view-models and the real
// Mediator pipeline, with a round-tripping settings store (a SaveAsync is reflected in the next LoadAsync).
// The bug: each settings section loaded a settings snapshot once and later committed its single-field change
// by writing that WHOLE snapshot back, silently reverting anything changed elsewhere in between. Switching the
// model and then changing the theme reverted the persisted model, so Home (which reads settings) and Model
// (which reads the loaded model) disagreed about the active model. Each scenario opens a section, changes the
// model elsewhere, then makes that section's own change, and asserts the model change survived and that Home
// and Model agree.

using Application.Display;
using Application.Ports;
using Application.Settings;
using AwesomeAssertions;
using Domain.Settings;
using Logic.AppManagement.Shell;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class SettingsWriteIsolationDriver
{
	private const string SecondDisplay = "\\\\.\\DISPLAY2";

	private readonly IMediator _mediator;
	private readonly IServiceProvider _services;

	private AppSettings _persisted = AppSettings.Default;
	private Func<Task>? _sectionChange;

	public SettingsWriteIsolationDriver(IMediator mediator, ISettingsStore store, IMonitorCatalog catalog, IServiceProvider services)
	{
		_mediator = mediator;
		_services = services;

		store.LoadAsync(Arg.Any<CancellationToken>()).Returns(_ => _persisted);
		store.When(s => s.SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>()))
			.Do(call => _persisted = call.Arg<AppSettings>());

		// A second display so the General section has a genuine overlay-display change to commit.
		catalog.GetMonitors().Returns([
			new MonitorInfo("\\\\.\\DISPLAY1", "Primary display (1920 × 1080)", IsPrimary: true, 0, 0, 1920, 1040),
			new MonitorInfo(SecondDisplay, "Display 2 (1920 × 1080)", IsPrimary: false, 1920, 0, 1920, 1040)]);
	}

	public void GivenActiveModel(string modelId) => _persisted = WithModel(_persisted, modelId);

	// Load the section the way the shell does, so it holds its settings snapshot, and remember how that
	// section commits its own single-field change.
	public async Task OpenSection(string section)
	{
		switch (section)
		{
			case "Theme":
				ThemeViewModel theme = _services.GetRequiredService<ThemeViewModel>();
				await theme.LoadCommand.ExecuteAsync(null);
				_sectionChange = () =>
				{
					theme.SelectedTheme = ThemePreference.Dark;
					return Task.CompletedTask;
				};
				break;
			case "General":
				GeneralViewModel general = _services.GetRequiredService<GeneralViewModel>();
				await general.LoadCommand.ExecuteAsync(null);
				_sectionChange = () =>
				{
					general.SelectedOverlayMonitor = SecondDisplay;
					return Task.CompletedTask;
				};
				break;
			case "Audio":
				AudioDeviceViewModel audio = _services.GetRequiredService<AudioDeviceViewModel>();
				await audio.LoadCommand.ExecuteAsync(null);
				_sectionChange = () => audio.SelectCommand.ExecuteAsync("mic-b");
				break;
			case "Hotkey":
				HotkeyViewModel hotkey = _services.GetRequiredService<HotkeyViewModel>();
				await hotkey.LoadCommand.ExecuteAsync(null);
				_sectionChange = () => hotkey.AssignCommand.ExecuteAsync("F13");
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(section), section, "Unknown settings section.");
		}
	}

	// A model change made somewhere other than the open section, through the same settings pipeline.
	public async Task SwitchModelElsewhere(string modelId)
	{
		AppSettingsDto current = await _mediator.Send(new GetSettingsQuery());
		await _mediator.Send(new UpdateSettingsCommand(current with { ModelId = modelId }));
	}

	public async Task MakeSectionChange()
	{
		_sectionChange.Should().NotBeNull("a section must be opened first");
		await _sectionChange!();

		// The theme and overlay-display commits are fire-and-forget from a property setter; let them land.
		for (int attempt = 0; attempt < 50 && !SectionChangeLanded(); attempt++)
		{
			await Task.Delay(10);
		}
	}

	public void AssertSectionChangePersisted(string section)
	{
		switch (section)
		{
			case "Theme":
				_persisted.ThemePreference.Should().Be(ThemePreference.Dark);
				break;
			case "General":
				_persisted.OverlayMonitorDeviceName.Should().Be(SecondDisplay);
				break;
			case "Audio":
				_persisted.CaptureDeviceId.Should().Be("mic-b");
				break;
			case "Hotkey":
				_persisted.Hotkey.Chord.Should().Be("F13");
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(section), section, "Unknown settings section.");
		}
	}

	public void AssertPersistedModel(string modelId) =>
		_persisted.ModelId.Should().Be(modelId, "a section's own change must not revert a model switched elsewhere");

	public async Task AssertHomeAndModelAgree(string modelId)
	{
		HomeViewModel home = _services.GetRequiredService<HomeViewModel>();
		await home.RefreshCommand.ExecuteAsync(null);

		ModelViewModel model = _services.GetRequiredService<ModelViewModel>();
		await model.LoadCommand.ExecuteAsync(null);

		home.ActiveModel.Should().Be(modelId, "Home reports the persisted model");
		model.ActiveModelId.Should().Be(modelId, "the Model section agrees with Home");
	}

	private bool SectionChangeLanded() =>
		_persisted.ThemePreference == ThemePreference.Dark
		|| _persisted.OverlayMonitorDeviceName == SecondDisplay
		|| _persisted.CaptureDeviceId == "mic-b"
		|| _persisted.Hotkey.Chord == "F13";

	private static AppSettings WithModel(AppSettings settings, string modelId) => new(
		modelId,
		settings.Hotkey,
		settings.SilenceThresholdMs,
		settings.FillerWordRemovalEnabled,
		settings.CaptureDeviceId,
		settings.AuditLogEnabled,
		settings.SetupCompleted,
		themePreference: settings.ThemePreference,
		captureDeviceName: settings.CaptureDeviceName,
		overlayMonitorDeviceName: settings.OverlayMonitorDeviceName);
}
