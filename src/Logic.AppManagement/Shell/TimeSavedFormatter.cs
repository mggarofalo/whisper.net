// Formats the estimated typing time saved as a benefit ("4h 23m"), not a stopwatch ("04:23:37"). The
// dashboard figures are coarse estimates, so seconds are shown only while the total is under a minute and
// hours are never rolled into days. Lives in Logic (not a XAML StringFormat) so Home and Stats share one
// rule and it is unit-tested.

namespace Logic.AppManagement.Shell;

public static class TimeSavedFormatter
{
	public static string Format(TimeSpan duration)
	{
		if (duration < TimeSpan.Zero)
		{
			duration = TimeSpan.Zero;
		}

		int hours = (int)duration.TotalHours;
		if (hours > 0)
		{
			return $"{hours}h {duration.Minutes}m";
		}

		return duration.Minutes > 0 ? $"{duration.Minutes}m" : $"{duration.Seconds}s";
	}
}
