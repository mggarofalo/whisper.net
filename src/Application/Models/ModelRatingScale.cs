// Derives a model's speed/accuracy/memory ratings from its on-disk size (WHISPER-27). Size is a strong,
// objective proxy for all three: a bigger Whisper model is more accurate but slower and needs more
// memory, and a smaller one is the reverse. Two size thresholds split the catalog into small / medium /
// large tiers, and the ratings track the tier. Pure and deterministic, so the picker's guidance never
// drifts and needs no per-model hand-authoring.

namespace Application.Models;

public static class ModelRatingScale
{
	private const long Megabyte = 1024L * 1024L;

	// Tier boundaries: below ~200 MB is "small" (tiny/base), below ~1 GB is "medium" (small), and the
	// rest is "large" (medium/large-v3).
	private const long SmallTierMaxBytes = 200 * Megabyte;
	private const long MediumTierMaxBytes = 1000 * Megabyte;

	public static ModelRatings From(long sizeBytes)
	{
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
}
