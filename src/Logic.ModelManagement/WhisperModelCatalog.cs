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

	// The standard ggml models published at huggingface.co/ggerganov/whisper.cpp. Sizes are the
	// published approximate on-disk sizes; hashes are left empty (verified as a non-empty download).
	//
	// The two turbo entries are large-v3 with a pruned decoder (4 layers instead of 32). They keep
	// large-v3's encoder — so near-large accuracy — while transcribing several times faster, which makes
	// turbo the best default for dictation on capable hardware. The q5_0 build is the same model quantized:
	// roughly a third of the download and memory for a small accuracy cost. Because their speed comes from
	// architecture rather than size, ModelRatingScale rates the turbo family explicitly instead of inferring
	// speed from bytes on disk.
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
		new("large-v3-turbo-q5_0", "Large v3 Turbo (multilingual, compressed)", "q5_0", "ggml-large-v3-turbo-q5_0.bin", 547 * Mb),
		new("large-v3-turbo", "Large v3 Turbo (multilingual)", "f16", "ggml-large-v3-turbo.bin", 1_550 * Mb),
		new("large-v3", "Large v3 (multilingual)", "f16", "ggml-large-v3.bin", 2_900 * Mb),

		// Distil-Whisper distil-large-v3.5 (MIT): large-v3's encoder with a 2-layer distilled decoder, trained on
		// English only, so it is pinned to "en" (it keeps the multilingual tokenizer, so auto-detection could
		// wander). Roughly 1.5x turbo's speed with slightly better short-form accuracy, which suits dictation. It
		// lives in its own Hugging Face repo under a generic file name, so it carries its download URL and is
		// cached under a distinct local name; the published SHA-256 is pinned.
		new(
			"distil-large-v3.5",
			"Distil Large v3.5 (English)",
			"f16",
			"ggml-distil-large-v3.5.bin",
			1_519_521_155,
			sha256: "ec2498919b498c5f6b00041adb45650124b3cd9f26f545fffa8f5d11c28dcf26",
			sourceUrl: "https://huggingface.co/distil-whisper/distil-large-v3.5-ggml/resolve/main/ggml-model.bin",
			language: "en"),
	];

	public IReadOnlyList<WhisperModelCatalogEntry> Entries => CatalogEntries;

	public WhisperModelCatalogEntry? Find(string id) =>
		CatalogEntries.FirstOrDefault(entry => string.Equals(entry.Id, id, StringComparison.OrdinalIgnoreCase));
}
