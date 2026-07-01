// Smoke for the real Win32 monitor catalog: it is the display-topology I/O boundary the overlay and the
// General picker depend on, so this pins that enumeration works against the host's actual displays —
// returns at least one monitor, exactly one primary, and a usable work area and device name for each. The
// on-screen placement that consumes these coordinates is the manual remainder, as with the overlay smoke.

using System.Linq;
using Application.Display;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Presentation.Overlay;
using Xunit;

namespace Presentation.Smoke.Tests;

public sealed class MonitorCatalogSmokeTests
{
	[Fact]
	public void Enumerates_the_host_displays_with_exactly_one_primary()
	{
		Win32MonitorCatalog catalog = new(NullLogger<Win32MonitorCatalog>.Instance);

		IReadOnlyList<MonitorInfo> monitors = catalog.GetMonitors();

		monitors.Should().NotBeEmpty("the test host has at least one display");
		monitors.Count(monitor => monitor.IsPrimary).Should().Be(1, "Windows reports exactly one primary display");
		monitors[0].IsPrimary.Should().BeTrue("the catalog lists the primary first so the picker/fallback agree");

		foreach (MonitorInfo monitor in monitors)
		{
			monitor.DeviceName.Should().NotBeNullOrWhiteSpace("each display has a device name to persist against");
			monitor.WorkAreaWidth.Should().BeGreaterThan(0, "each display has a usable work area");
			monitor.WorkAreaHeight.Should().BeGreaterThan(0, "each display has a usable work area");
		}
	}
}
