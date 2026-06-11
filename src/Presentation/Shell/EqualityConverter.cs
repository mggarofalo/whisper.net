// Compares two bound values for equality, for the nav region's selected-item visual (WHISPER-103): the
// nav button's DataContext is its section key, and the second value is the shell's CurrentSectionKey, so
// the multi-binding yields true on the active section's button. String comparison is case-insensitive to
// match the navigation service's case-insensitive section keys. Lives in Presentation because it is pure
// view glue.

using System.Globalization;
using System.Windows.Data;

namespace Presentation.Shell;

public sealed class EqualityConverter : IMultiValueConverter
{
	public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture) =>
		values.Length == 2 && string.Equals(values[0] as string, values[1] as string, StringComparison.OrdinalIgnoreCase);

	public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
		throw new NotSupportedException();
}
