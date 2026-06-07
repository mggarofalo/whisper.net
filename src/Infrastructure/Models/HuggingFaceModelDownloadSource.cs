// The real model byte source: Hugging Face's whisper.cpp repository, the canonical home of the ggml
// Whisper models. It streams the file for a catalog entry over HTTPS and reports the Content-Length so
// the downloader can show accurate progress. This endpoint is the app's single model-related network
// access, and it is reached only when the user explicitly requests a download (see README).

using Domain.Models;

namespace Infrastructure.Models;

internal sealed class HuggingFaceModelDownloadSource(HttpClient httpClient) : IModelDownloadSource
{
	// Public, unauthenticated model files; "resolve/main" returns the raw bytes (following LFS redirects).
	private const string BaseUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/";

	public async ValueTask<ModelDownload> OpenAsync(WhisperModelCatalogEntry entry, CancellationToken cancellationToken)
	{
		HttpResponseMessage response = await httpClient
			.GetAsync(BaseUrl + entry.FileName, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
			.ConfigureAwait(false);
		response.EnsureSuccessStatusCode();

		long? total = response.Content.Headers.ContentLength;
		Stream content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
		return new ModelDownload(content, total);
	}
}
