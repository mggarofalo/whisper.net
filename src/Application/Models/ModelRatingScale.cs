// Derives a model's speed/accuracy/memory ratings from its size and id. Size is a strong,
// objective proxy for all three within a model family: a bigger Whisper model is more accurate but slower
// and needs more memory, and a smaller one is the reverse. Two size thresholds split the catalog into
// small / medium / large tiers, and the ratings track the tier. Pure and deterministic, so the picker's
// guidance never drifts and needs no per-model hand-authoring.
//
// The turbo family is the one place size alone misleads. large-v3-turbo keeps large-v3's full encoder but
// prunes the decoder from 32 layers to 4: it lands in the large size tier yet transcribes several times
// faster than large-v3. Rating it by bytes would label the app's fastest accurate model "Speed: Low" and
// steer users away from the best choice for dictation, so the family is rated explicitly. Memory still
// follows the size tier, because that part of the size proxy still holds.

namespace Application.Models;

public static class ModelRatingScale
{
	private const long Megabyte = 1024L * 1024L;

	// Marks the pruned-decoder variants (large-v3-turbo and its quantizations) by id.
	private const string TurboMarker = "turbo";

	// Tier boundaries: below ~200 MB is "small" (tiny/base), below ~1 GB is "medium" (small), and the
	// rest is "large" (medium/large-v3).
	private const long SmallTierMaxBytes = 200 * Megabyte;
	private const long MediumTierMaxBytes = 1000 * Megabyte;

	public static ModelRatings From(long sizeBytes, string modelId)
	{
		if (IsTurbo(modelId))
		{
			// Near-large accuracy at a fraction of the decode cost; memory still tracks the download size.
			return new ModelRatings(
				Speed: ModelRating.High,
				Accuracy: ModelRating.High,
				Memory: MemoryFor(sizeBytes));
		}

		if (sizeBytes < SmallTierMaxBytes)
		{
			// Small: fastest, lightest, least accurate.
			return new ModelRatings(Speed: ModelRating.High, Accuracy: ModelRating.Low, Memory: ModelRating.Low);
		}

		if (sizeBytes < MediumTierMaxBytes)
		{
			return new ModelRatings(Speed: ModelRating.Medium, Accuracy: ModelRating.Medium, Memory: ModelRating.Medium);
		}

		// Large: most accurate, slowest, heaviest.
		return new ModelRatings(Speed: ModelRating.Low, Accuracy: ModelRating.High, Memory: ModelRating.High);
	}

	private static bool IsTurbo(string modelId) =>
		!string.IsNullOrEmpty(modelId) && modelId.Contains(TurboMarker, StringComparison.OrdinalIgnoreCase);

	private static ModelRating MemoryFor(long sizeBytes) => sizeBytes switch
	{
		< SmallTierMaxBytes => ModelRating.Low,
		< MediumTierMaxBytes => ModelRating.Medium,
		_ => ModelRating.High,
	};
}
