// Port for the local model cache: where downloaded GGUF files live on disk. It answers "is this model
// already here?" purely from the filesystem — no network call — and yields the path a cached model
// occupies (or would occupy). Implemented in Infrastructure; faked in specs so cache state can be set
// up without touching the real cache directory.

using Domain.Models;

namespace Application.Ports;

public interface IModelCache
{
	/// <summary>Reports whether the model's file already exists in the local cache (no network).</summary>
	bool IsCached(WhisperModelCatalogEntry entry);

	/// <summary>The local path the model's file occupies (or would occupy once downloaded).</summary>
	string GetCachedPath(WhisperModelCatalogEntry entry);
}
