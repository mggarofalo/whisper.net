// A progress report emitted while a model downloads: how many bytes have arrived and, when known, the
// total to expect. Percent is null while the total is unknown (a server that omits Content-Length), so
// the UI can show an indeterminate state rather than a misleading number.

namespace Domain.Models;

public sealed record ModelDownloadProgress(long BytesDownloaded, long? TotalBytes)
{
	/// <summary>Completion as a 0–100 percentage, or null when the total size is not yet known.</summary>
	public double? Percent => TotalBytes is long total and > 0
		? Math.Min(100d, (double)BytesDownloaded / total * 100d)
		: null;
}
