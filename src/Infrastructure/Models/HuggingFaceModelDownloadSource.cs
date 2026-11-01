// The real model byte source: Hugging Face. The standard ggml Whisper models come from the whisper.cpp
// repository; an entry from another repository (Distil-Whisper) carries its own URL. It streams the file for a catalog entry over HTTPS and reports the Content-Length so
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
			.GetAsync(UrlFor(entry), HttpCompletionOption.ResponseHeadersRead, cancellationToken)
			.ConfigureAwait(false);
		response.EnsureSuccessStatusCode();

		long? total = response.Content.Headers.ContentLength;
		Stream content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
		return new ModelDownload(content, total);
	}

	// An entry from another Hugging Face repository names its own URL; the standard models live in the
	// whisper.cpp repository under their file name.
	internal static string UrlFor(WhisperModelCatalogEntry entry) =>
		string.IsNullOrWhiteSpace(entry.SourceUrl) ? BaseUrl + entry.FileName : entry.SourceUrl;
}
