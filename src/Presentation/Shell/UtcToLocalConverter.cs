// Converts a UTC DateTimeOffset to the user's local time, for display only (WHISPER-115). History entries
// (and the Home dashboard's recent list) store and query their timestamps in UTC — correct — but a
// StringFormat on a UTC DateTimeOffset renders the UTC wall-clock, so a 9pm-local dictation showed as the
// next day. This converter is applied in the timestamp bindings so the local conversion happens only at
// the display boundary; the WPF-free view-models stay UTC and testable, and storage/queries are untouched.

using System;
using System.Globalization;
using System.Windows.Data;

namespace Presentation.Shell;

public sealed class UtcToLocalConverter : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		value is DateTimeOffset utc ? utc.ToLocalTime() : value;

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		throw new NotSupportedException();
}
