// The local model cache over the real filesystem. It answers "is this model already here?" with a
// single File.Exists check against the cache directory — no network — and yields the path a model
// occupies (or would occupy once downloaded).

using Application.Ports;
using Domain.Models;
using Microsoft.Extensions.Options;

namespace Infrastructure.Models;

public sealed class FileSystemModelCache(IOptions<ModelCacheOptions> options) : IModelCache
{
	private readonly string _cacheDirectory = options.Value.CacheDirectory;

	public bool IsCached(WhisperModelCatalogEntry entry) => File.Exists(GetCachedPath(entry));

	public string GetCachedPath(WhisperModelCatalogEntry entry) => Path.Combine(_cacheDirectory, entry.FileName);
}
