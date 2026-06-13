// Pins the display-boundary conversion: history/dashboard timestamps are stored UTC but
// rendered in the user's local time. The converter must return the SAME instant in the local offset, so
// the wall-clock shown is local — independent of the host's time zone (asserted against TimeZoneInfo.Local).

using System;
using System.Globalization;
using AwesomeAssertions;
using Presentation.Shell;
using Xunit;

namespace Presentation.Smoke.Tests;

public sealed class UtcToLocalConverterTests
{
	private readonly UtcToLocalConverter _converter = new();

	[Fact]
	public void Converts_a_utc_offset_to_the_local_offset_for_the_same_instant()
	{
		DateTimeOffset utc = new(2026, 6, 11, 2, 0, 0, TimeSpan.Zero); // 02:00 UTC

		object? result = _converter.Convert(utc, typeof(string), null, CultureInfo.InvariantCulture);

		result.Should().BeOfType<DateTimeOffset>();
		DateTimeOffset local = (DateTimeOffset)result!;
		local.UtcDateTime.Should().Be(utc.UtcDateTime, "the conversion is display-only — the same instant");
		local.Offset.Should().Be(TimeZoneInfo.Local.GetUtcOffset(utc), "the timestamp is shown in the local time zone");
	}

	[Fact]
	public void Passes_through_a_non_datetimeoffset_value()
	{
		_converter.Convert("not a date", typeof(string), null, CultureInfo.InvariantCulture).Should().Be("not a date");
		_converter.Convert(null, typeof(string), null, CultureInfo.InvariantCulture).Should().BeNull();
	}
}
