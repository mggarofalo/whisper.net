// Drives the overlay-display picker over the REAL GeneralViewModel and Mediator pipeline (GetSettings +
// ListMonitors to load, UpdateSettings to persist + broadcast), with a round-tripping settings store (a
// SaveAsync is reflected in the next LoadAsync) and the substituted monitor catalog reconfigured per
// scenario. So choosing a display is observed as the persisted OverlayMonitorDeviceName and survives a
// reload. The primary is represented by the "Primary display (default)" choice (persisted as null); other
// displays are listed by name. Placing the real overlay window on the chosen monitor is the App/WPF job
// and a manual remainder.

using Application.Display;
using Application.Ports;
using AwesomeAssertions;
using Domain.Settings;
using Logic.AppManagement.Shell;
using Mediator;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class OverlayDisplayDriver
{
	public const string SecondaryDeviceName = "\\\\.\\DISPLAY2";

	private static readonly MonitorInfo Primary =
		new("\\\\.\\DISPLAY1", "Primary display (1920 × 1080)", IsPrimary: true, 0, 0, 1920, 1040);

	private static readonly MonitorInfo Secondary =
		new(SecondaryDeviceName, "Display 2 (1920 × 1080)", IsPrimary: false, 1920, 0, 1920, 1040);

	private readonly IMediator _mediator;
	private readonly IMonitorCatalog _catalog;
	private readonly GeneralViewModel _viewModel;

	private AppSettings _persisted = AppSettings.Default;

	public OverlayDisplayDriver(IMediator mediator, ISettingsStore store, IMonitorCatalog catalog)
	{
		_mediator = mediator;
		_catalog = catalog;

		// Round-trip the store so a persisted choice is observed on the next load and survives a reload.
		store.LoadAsync(Arg.Any<CancellationToken>()).Returns(_ => _persisted);
		store.When(s => s.SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>()))
			.Do(call => _persisted = call.Arg<AppSettings>());

		// Default: a single primary display.
		_catalog.GetMonitors().Returns([Primary]);

		_viewModel = new GeneralViewModel(mediator);
	}

	public void GivenASecondDisplayIsAttached() => _catalog.GetMonitors().Returns([Primary, Secondary]);

	public Task OpenSection() => _viewModel.LoadCommand.ExecuteAsync(null);

	public void SelectSecondDisplay() => _viewModel.SelectedOverlayMonitor = SecondaryDeviceName;

	public void AssertPrimaryDefaultOffered() =>
		_viewModel.OverlayMonitors.Should().Contain(option => option.DeviceName == null,
			"the primary default is always offered as the first choice");

	public void AssertSelectionFollowsPrimary() =>
		_viewModel.SelectedOverlayMonitor.Should().BeNull("a fresh install follows the primary display by default");

	public void AssertSecondDisplayListed() =>
		_viewModel.OverlayMonitors.Should().Contain(option => option.DeviceName == SecondaryDeviceName,
			"an attached non-primary display is offered by name");

	public void AssertPersistedIsSecondDisplay() =>
		_persisted.OverlayMonitorDeviceName.Should().Be(SecondaryDeviceName, "the chosen display is saved");

	public async Task AssertReopeningShowsSecondDisplay()
	{
		GeneralViewModel reopened = new(_mediator);
		await reopened.LoadCommand.ExecuteAsync(null);
		reopened.SelectedOverlayMonitor.Should().Be(SecondaryDeviceName, "the persisted display survives a reload");
	}
}
