// Port for acquiring a missing model: downloads it from Hugging Face into the local cache and verifies
// its integrity before reporting it available. This is the one model-related network egress, and it is
// always user-initiated (opt-in) — nothing here is invoked automatically. Implemented in
// Infrastructure; faked in specs so the download flow can be driven without the network.

using Domain.Models;

namespace Application.Ports;

public interface IModelDownloader
{
	/// <summary>
	/// Downloads <paramref name="entry"/> into the cache, reporting byte/percent progress, and returns
	/// the verified file's local path. A cancellation aborts cleanly, leaving no partial cache file.
	/// </summary>
	ValueTask<string> DownloadAsync(
		WhisperModelCatalogEntry entry,
		IProgress<ModelDownloadProgress>? progress,
		CancellationToken cancellationToken);
}
