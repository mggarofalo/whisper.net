// Inner TDD loop for the WHISPER-4 downloader, over a fake byte source and a real filesystem cache in a
// temp directory (no network). Confirms it streams to the cache and reports progress, verifies SHA-256
// when the catalog supplies one (accepting a match, rejecting a mismatch), rejects an empty download,
// and — on a mismatch or cancellation — leaves no partial/corrupt file behind.

using System.Security.Cryptography;
using System.Text;
using Application.Ports;
using AwesomeAssertions;
using Domain.Models;
using Infrastructure.Models;
using Microsoft.Extensions.Options;
using Xunit;

namespace Infrastructure.Tests.Models;

public sealed class ModelDownloaderTests : IDisposable
{
	private readonly string _cacheDir = Path.Combine(Path.GetTempPath(), $"whisper-cache-{Guid.NewGuid():N}");
	private readonly IModelCache _cache;

	public ModelDownloaderTests() =>
		_cache = new FileSystemModelCache(Options.Create(new ModelCacheOptions { CacheDirectory = _cacheDir }));

	private static WhisperModelCatalogEntry Entry(long size, string sha256 = "") =>
		new("base", "Base", "f16", "ggml-base.bin", size, sha256);

	private static string Sha256Hex(byte[] bytes) =>
		Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

	private sealed class CollectingProgress : IProgress<ModelDownloadProgress>
	{
		public List<ModelDownloadProgress> Reports { get; } = [];

		public void Report(ModelDownloadProgress value) => Reports.Add(value);
	}

	[Fact]
	public async Task Streams_to_the_cache_and_reports_progress()
	{
		byte[] bytes = Encoding.UTF8.GetBytes(new string('x', 5_000));
		ModelDownloader downloader = new(new FakeModelDownloadSource(bytes, bytes.Length), _cache);
		CollectingProgress progress = new();

		string path = await downloader.DownloadAsync(Entry(bytes.Length), progress, CancellationToken.None);

		path.Should().Be(Path.Combine(_cacheDir, "ggml-base.bin"));
		File.Exists(path).Should().BeTrue();
		File.Exists(path + ".part").Should().BeFalse();
		progress.Reports.Should().NotBeEmpty();
		progress.Reports[^1].BytesDownloaded.Should().Be(bytes.Length);
		progress.Reports[^1].Percent.Should().Be(100d);
	}

	[Fact]
	public async Task Accepts_a_matching_sha256()
	{
		byte[] bytes = Encoding.UTF8.GetBytes("known-good-model");
		ModelDownloader downloader = new(new FakeModelDownloadSource(bytes, bytes.Length), _cache);

		string path = await downloader.DownloadAsync(Entry(bytes.Length, Sha256Hex(bytes)), progress: null, CancellationToken.None);

		File.Exists(path).Should().BeTrue();
	}

	[Fact]
	public async Task Rejects_a_mismatched_sha256_and_leaves_no_file()
	{
		byte[] bytes = Encoding.UTF8.GetBytes("tampered");
		ModelDownloader downloader = new(new FakeModelDownloadSource(bytes, bytes.Length), _cache);
		string wrongHash = Sha256Hex(Encoding.UTF8.GetBytes("expected-something-else"));

		Func<Task> act = async () => await downloader.DownloadAsync(Entry(bytes.Length, wrongHash), null, CancellationToken.None);

		await act.Should().ThrowAsync<ModelLoadException>();
		File.Exists(Path.Combine(_cacheDir, "ggml-base.bin")).Should().BeFalse();
		File.Exists(Path.Combine(_cacheDir, "ggml-base.bin.part")).Should().BeFalse();
	}

	[Fact]
	public async Task Rejects_an_empty_download()
	{
		ModelDownloader downloader = new(new FakeModelDownloadSource([], 0), _cache);

		Func<Task> act = async () => await downloader.DownloadAsync(Entry(0), null, CancellationToken.None);

		await act.Should().ThrowAsync<ModelLoadException>();
		File.Exists(Path.Combine(_cacheDir, "ggml-base.bin")).Should().BeFalse();
	}

	[Fact]
	public async Task Aborts_cleanly_on_cancellation_leaving_no_partial_file()
	{
		byte[] bytes = Encoding.UTF8.GetBytes(new string('y', 5_000));
		ModelDownloader downloader = new(new FakeModelDownloadSource(bytes, bytes.Length), _cache);

		Func<Task> act = async () =>
			await downloader.DownloadAsync(Entry(bytes.Length), null, new CancellationToken(canceled: true));

		await act.Should().ThrowAsync<OperationCanceledException>();
		File.Exists(Path.Combine(_cacheDir, "ggml-base.bin")).Should().BeFalse();
		File.Exists(Path.Combine(_cacheDir, "ggml-base.bin.part")).Should().BeFalse();
	}

	public void Dispose()
	{
		if (Directory.Exists(_cacheDir))
		{
			Directory.Delete(_cacheDir, recursive: true);
		}
	}
}
