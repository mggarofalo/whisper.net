// Inner TDD loop for the cache detector: it reports a model present when its file exists in
// the cache directory and absent otherwise, using only the filesystem (no network), and resolves the
// path a model occupies. Driven against a real temp directory.

using Application.Ports;
using AwesomeAssertions;
using Domain.Models;
using Infrastructure.Models;
using Microsoft.Extensions.Options;
using Xunit;

namespace Infrastructure.Tests.Models;

public sealed class FileSystemModelCacheTests : IDisposable
{
	private readonly string _cacheDir = Path.Combine(Path.GetTempPath(), $"whisper-cache-{Guid.NewGuid():N}");
	private readonly IModelCache _cache;
	private static readonly WhisperModelCatalogEntry Base = new("base", "Base", "f16", "ggml-base.bin", 100);

	public FileSystemModelCacheTests()
	{
		Directory.CreateDirectory(_cacheDir);
		_cache = new FileSystemModelCache(Options.Create(new ModelCacheOptions { CacheDirectory = _cacheDir }));
	}

	[Fact]
	public void Reports_a_model_as_cached_when_its_file_exists()
	{
		File.WriteAllText(Path.Combine(_cacheDir, "ggml-base.bin"), "model-bytes");

		_cache.IsCached(Base).Should().BeTrue();
	}

	[Fact]
	public void Reports_a_model_as_not_cached_when_its_file_is_absent() =>
		_cache.IsCached(Base).Should().BeFalse();

	[Fact]
	public void Resolves_the_path_a_model_occupies() =>
		_cache.GetCachedPath(Base).Should().Be(Path.Combine(_cacheDir, "ggml-base.bin"));

	public void Dispose()
	{
		if (Directory.Exists(_cacheDir))
		{
			Directory.Delete(_cacheDir, recursive: true);
		}
	}
}
