// Where downloaded model files are cached on disk. Defaults (when left unset) to a per-user folder
// under LocalApplicationData, resolved at registration time. Configurable so tests can point the cache
// at a temp directory and users can relocate it.

namespace Infrastructure.Models;

public sealed class ModelCacheOptions
{
	public const string SectionName = "ModelCache";

	/// <summary>Directory holding cached GGUF model files. Empty means "use the per-user default".</summary>
	public string CacheDirectory { get; set; } = string.Empty;
}
