// Formats a model's download size the way Windows shows file sizes ("142 MB", "1.5 GB"): binary units,
// whole megabytes below a gigabyte and one decimal above. Lives in Logic (not a XAML StringFormat) so the
// rule is unit-tested.

using System.Globalization;

namespace Logic.AppManagement.Shell;

public static class ModelSizeFormatter
{
	private const double Megabyte = 1024d * 1024d;
	private const double Gigabyte = Megabyte * 1024d;

	public static string Format(long bytes)
	{
		if (bytes < 0)
		{
			bytes = 0;
		}

		// Round first so a size just under a gigabyte reads "1.0 GB" rather than "1024 MB".
		double megabytes = Math.Round(bytes / Megabyte);
		return megabytes < 1024
			? string.Create(CultureInfo.InvariantCulture, $"{megabytes:0} MB")
			: string.Create(CultureInfo.InvariantCulture, $"{bytes / Gigabyte:0.0} GB");
	}
}
