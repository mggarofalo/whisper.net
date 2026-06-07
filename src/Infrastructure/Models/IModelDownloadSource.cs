// Infrastructure-internal seam over the raw byte source of a model download. It isolates the network
// (Hugging Face over HTTP) from ModelDownloader's orchestration — streaming to a temp file, reporting
// progress, verifying integrity, and the atomic move into the cache — so that orchestration can be
// unit-tested with a fake source and no network. The real implementation is HuggingFaceModelDownloadSource.

using Domain.Models;

namespace Infrastructure.Models;

public interface IModelDownloadSource
{
	/// <summary>Opens the model's bytes for reading, reporting the total length when the source knows it.</summary>
	ValueTask<ModelDownload> OpenAsync(WhisperModelCatalogEntry entry, CancellationToken cancellationToken);
}

public sealed class ModelDownload(Stream content, long? totalBytes) : IAsyncDisposable
{
	public Stream Content { get; } = content;

	/// <summary>Total bytes to expect, or null when the source does not report a length.</summary>
	public long? TotalBytes { get; } = totalBytes;

	public ValueTask DisposeAsync() => Content.DisposeAsync();
}
