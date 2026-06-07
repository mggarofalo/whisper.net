// The registry of Whisper models the app supports — pure, on-device data (no network). Each entry
// names a ggml variant, its quantization, the canonical on-disk file name (ggml-<id>.bin, matching the
// Hugging Face whisper.cpp repo), and an approximate size used to drive download progress. Sizes are
// approximate and informational; integrity on download is verified separately. This is the single
// source of truth the cache and downloader reason about.

using Application.Ports;
using Domain.Models;

namespace Logic.ModelManagement;

public sealed class WhisperModelCatalog : IModelCatalog
{
	private const long Mb = 1024L * 1024L;

	// The standard f16 ggml models published at huggingface.co/ggerganov/whisper.cpp. Sizes are the
	// published approximate on-disk sizes; hashes are left empty (verified as a non-empty download).
	private static readonly IReadOnlyList<WhisperModelCatalogEntry> CatalogEntries =
	[
		new("tiny", "Tiny (multilingual)", "f16", "ggml-tiny.bin", 75 * Mb),
		new("tiny.en", "Tiny (English)", "f16", "ggml-tiny.en.bin", 75 * Mb),
		new("base", "Base (multilingual)", "f16", "ggml-base.bin", 142 * Mb),
		new("base.en", "Base (English)", "f16", "ggml-base.en.bin", 142 * Mb),
		new("small", "Small (multilingual)", "f16", "ggml-small.bin", 466 * Mb),
		new("small.en", "Small (English)", "f16", "ggml-small.en.bin", 466 * Mb),
		new("medium", "Medium (multilingual)", "f16", "ggml-medium.bin", 1_500 * Mb),
		new("medium.en", "Medium (English)", "f16", "ggml-medium.en.bin", 1_500 * Mb),
		new("large-v3", "Large v3 (multilingual)", "f16", "ggml-large-v3.bin", 2_900 * Mb),
	];

	public IReadOnlyList<WhisperModelCatalogEntry> Entries => CatalogEntries;

	public WhisperModelCatalogEntry? Find(string id) =>
		CatalogEntries.FirstOrDefault(entry => string.Equals(entry.Id, id, StringComparison.OrdinalIgnoreCase));
}
