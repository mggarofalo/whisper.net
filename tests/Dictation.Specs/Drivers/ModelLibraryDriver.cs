// The Driver owns HOW the model registry/cache/download flow is exercised: it runs the REAL catalog,
// the REAL filesystem cache (in a temp directory), and the REAL downloader over a recording byte source
// that never touches the network. Cache queries can therefore be shown to make no network request, and
// a download can be shown to report progress and leave a verified file in the cache — all hermetically.

using Application.Ports;
using AwesomeAssertions;
using Dictation.Specs.Support;
using Domain.Models;
using Infrastructure.Models;
using Logic.ModelManagement;
using Microsoft.Extensions.Options;

namespace Dictation.Specs.Drivers;

public sealed class ModelLibraryDriver : IDisposable
{
	private readonly string _cacheDir = Path.Combine(Path.GetTempPath(), $"whisper-spec-{Guid.NewGuid():N}");
	private readonly IModelCatalog _catalog = new WhisperModelCatalog();
	private readonly IModelCache _cache;
	private readonly RecordingModelDownloadSource _source = new();
	private readonly List<ModelDownloadProgress> _progress = [];

	private WhisperModelCatalogEntry _entry;
	private bool? _cached;
	private string? _downloadedPath;

	public ModelLibraryDriver()
	{
		Directory.CreateDirectory(_cacheDir);
		_cache = new FileSystemModelCache(Options.Create(new ModelCacheOptions { CacheDirectory = _cacheDir }));
		_entry = _catalog.Entries[0];
	}

	public void GivenModelAlreadyCached(string id)
	{
		_entry = Resolve(id);
		File.WriteAllText(_cache.GetCachedPath(_entry), "cached-model-bytes");
	}

	public void GivenModelNotCached(string id)
	{
		_entry = Resolve(id);
		string path = _cache.GetCachedPath(_entry);
		if (File.Exists(path))
		{
			File.Delete(path);
		}
	}

	public void QueryCacheStatus() => _cached = _cache.IsCached(_entry);

	public async Task RequestDownload()
	{
		ModelDownloader downloader = new(_source, _cache);
		_downloadedPath = await downloader.DownloadAsync(_entry, new SyncProgress(_progress), CancellationToken.None);
	}

	public void AssertReportedAvailable() => _cached.Should().BeTrue();

	public void AssertNoNetworkRequest() => _source.WasCalled.Should().BeFalse();

	public void AssertProgressReportedUntilCompletion()
	{
		_progress.Should().NotBeEmpty();
		_progress[^1].Percent.Should().Be(100d);
	}

	public void AssertVerifiedFileInCache()
	{
		_downloadedPath.Should().NotBeNull();
		File.Exists(_downloadedPath!).Should().BeTrue();
		_cache.IsCached(_entry).Should().BeTrue();
	}

	private WhisperModelCatalogEntry Resolve(string id) =>
		_catalog.Find(id) ?? throw new InvalidOperationException($"Unknown model id '{id}'.");

	public void Dispose()
	{
		if (Directory.Exists(_cacheDir))
		{
			Directory.Delete(_cacheDir, recursive: true);
		}
	}

	// Captures progress synchronously (inline with each Report call), unlike Progress<T> which marshals
	// to a captured context and would race the assertions.
	private sealed class SyncProgress(List<ModelDownloadProgress> sink) : IProgress<ModelDownloadProgress>
	{
		public void Report(ModelDownloadProgress value) => sink.Add(value);
	}
}
