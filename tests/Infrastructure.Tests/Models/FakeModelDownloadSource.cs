// A fake byte source for driving ModelDownloader without the network. It hands back the configured
// bytes (optionally reporting a total length, or a delay between chunks so cancellation can be exercised
// mid-stream) — the seam the real HuggingFaceModelDownloadSource implements.

using Domain.Models;
using Infrastructure.Models;

namespace Infrastructure.Tests.Models;

internal sealed class FakeModelDownloadSource(byte[] bytes, long? reportedTotal, bool reportTotal = true) : IModelDownloadSource
{
	public ValueTask<ModelDownload> OpenAsync(WhisperModelCatalogEntry entry, CancellationToken cancellationToken)
	{
		long? total = reportTotal ? reportedTotal ?? bytes.Length : null;
		return ValueTask.FromResult(new ModelDownload(new MemoryStream(bytes, writable: false), total));
	}
}
