// Unit tests for ListMonitorsHandler: the query that backs the General section's overlay-display picker.
// The handler is pure orchestration over the IMonitorCatalog port, so these pin that it surfaces exactly
// what the catalog reports (order and contents) and stays empty when no monitors are available.

using Application.Display;
using Application.Ports;
using NSubstitute;
using Xunit;

namespace Application.Tests.Display;

public sealed class ListMonitorsHandlerTests
{
	private readonly IMonitorCatalog _catalog = Substitute.For<IMonitorCatalog>();

	private ListMonitorsHandler NewHandler() => new(_catalog);

	[Fact]
	public async Task Returns_the_monitors_the_catalog_reports()
	{
		MonitorInfo primary = new("\\\\.\\DISPLAY1", "Primary display (1920 × 1080)", true, 0, 0, 1920, 1040);
		MonitorInfo secondary = new("\\\\.\\DISPLAY2", "Display 2 (2560 × 1440)", false, 1920, 0, 2560, 1400);
		_catalog.GetMonitors().Returns([primary, secondary]);

		IReadOnlyList<MonitorInfo> monitors = await NewHandler().Handle(new ListMonitorsQuery(), CancellationToken.None);

		Assert.Equal([primary, secondary], monitors);
	}

	[Fact]
	public async Task Returns_empty_when_the_catalog_reports_no_monitors()
	{
		_catalog.GetMonitors().Returns([]);

		IReadOnlyList<MonitorInfo> monitors = await NewHandler().Handle(new ListMonitorsQuery(), CancellationToken.None);

		Assert.Empty(monitors);
	}
}
