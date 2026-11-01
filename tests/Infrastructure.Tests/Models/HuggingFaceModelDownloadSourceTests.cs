// Pins where model bytes come from: the standard ggml models from the whisper.cpp repository under their file
// name, and an entry that names its own URL (Distil-Whisper) from that URL.

using AwesomeAssertions;
using Domain.Models;
using Infrastructure.Models;
using Xunit;

namespace Infrastructure.Tests.Models;

public sealed class HuggingFaceModelDownloadSourceTests
{
	[Fact]
	public void A_standard_model_comes_from_the_whisper_cpp_repository() =>
		HuggingFaceModelDownloadSource.UrlFor(new WhisperModelCatalogEntry("base.en", "Base", "f16", "ggml-base.en.bin", 1))
			.Should().Be("https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.en.bin");

	[Fact]
	public void An_entry_with_its_own_url_downloads_from_it()
	{
		const string url = "https://huggingface.co/distil-whisper/distil-large-v3.5-ggml/resolve/main/ggml-model.bin";

		HuggingFaceModelDownloadSource.UrlFor(new WhisperModelCatalogEntry("distil-large-v3.5", "Distil", "f16", "ggml-distil-large-v3.5.bin", 1, sourceUrl: url))
			.Should().Be(url);
	}
}