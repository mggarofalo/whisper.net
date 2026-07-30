// Verifies the model rating derivation: a model's speed/accuracy/memory ratings follow its
// size tier, except for the turbo family, whose pruned decoder makes it fast at a large size. Drives the
// real ModelRatingScale across the two tier boundaries so the small/medium/large branches — and the
// inverse relationship between speed and accuracy/memory — are each pinned.

using Application.Models;
using Xunit;

namespace Application.Tests.Models;

public sealed class ModelRatingScaleTests
{
	private const long Megabyte = 1024L * 1024L;

	[Theory]
	[InlineData(75 * Megabyte, "tiny")]
	[InlineData(142 * Megabyte, "base")]
	public void Small_models_are_fast_light_and_less_accurate(long sizeBytes, string modelId)
	{
		ModelRatings ratings = ModelRatingScale.From(sizeBytes, modelId);

		Assert.Equal(ModelRating.High, ratings.Speed);
		Assert.Equal(ModelRating.Low, ratings.Accuracy);
		Assert.Equal(ModelRating.Low, ratings.Memory);
	}

	[Theory]
	[InlineData(466 * Megabyte, "small")]
	public void Medium_models_rate_in_the_middle(long sizeBytes, string modelId)
	{
		ModelRatings ratings = ModelRatingScale.From(sizeBytes, modelId);

		Assert.Equal(ModelRating.Medium, ratings.Speed);
		Assert.Equal(ModelRating.Medium, ratings.Accuracy);
		Assert.Equal(ModelRating.Medium, ratings.Memory);
	}

	[Theory]
	[InlineData(1_500 * Megabyte, "medium")]
	[InlineData(2_900 * Megabyte, "large-v3")]
	public void Large_models_are_accurate_heavy_and_slower(long sizeBytes, string modelId)
	{
		ModelRatings ratings = ModelRatingScale.From(sizeBytes, modelId);

		Assert.Equal(ModelRating.Low, ratings.Speed);
		Assert.Equal(ModelRating.High, ratings.Accuracy);
		Assert.Equal(ModelRating.High, ratings.Memory);
	}

	// The turbo family keeps large-v3's encoder but prunes the decoder, so it is fast AND accurate. Rating
	// it by size alone would advertise the app's best dictation model as slow.
	[Theory]
	[InlineData(1_550 * Megabyte, "large-v3-turbo", ModelRating.High)]
	[InlineData(547 * Megabyte, "large-v3-turbo-q5_0", ModelRating.Medium)]
	public void Turbo_models_are_fast_and_accurate_with_memory_following_size(
		long sizeBytes, string modelId, ModelRating expectedMemory)
	{
		ModelRatings ratings = ModelRatingScale.From(sizeBytes, modelId);

		Assert.Equal(ModelRating.High, ratings.Speed);
		Assert.Equal(ModelRating.High, ratings.Accuracy);
		Assert.Equal(expectedMemory, ratings.Memory);
	}
}
