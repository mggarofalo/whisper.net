// Verifies the model rating derivation: a model's speed/accuracy/memory ratings follow its
// size tier. Drives the real ModelRatingScale across the two tier boundaries so the small/medium/large
// branches — and the inverse relationship between speed and accuracy/memory — are each pinned.

using Application.Models;
using Xunit;

namespace Application.Tests.Models;

public sealed class ModelRatingScaleTests
{
	private const long Megabyte = 1024L * 1024L;

	[Theory]
	[InlineData(75 * Megabyte)]   // tiny
	[InlineData(142 * Megabyte)]  // base
	public void Small_models_are_fast_light_and_less_accurate(long sizeBytes)
	{
		ModelRatings ratings = ModelRatingScale.From(sizeBytes);

		Assert.Equal(ModelRating.High, ratings.Speed);
		Assert.Equal(ModelRating.Low, ratings.Accuracy);
		Assert.Equal(ModelRating.Low, ratings.Memory);
	}

	[Theory]
	[InlineData(466 * Megabyte)]  // small
	public void Medium_models_rate_in_the_middle(long sizeBytes)
	{
		ModelRatings ratings = ModelRatingScale.From(sizeBytes);

		Assert.Equal(ModelRating.Medium, ratings.Speed);
		Assert.Equal(ModelRating.Medium, ratings.Accuracy);
		Assert.Equal(ModelRating.Medium, ratings.Memory);
	}

	[Theory]
	[InlineData(1_500 * Megabyte)]  // medium
	[InlineData(2_900 * Megabyte)]  // large-v3
	public void Large_models_are_accurate_heavy_and_slower(long sizeBytes)
	{
		ModelRatings ratings = ModelRatingScale.From(sizeBytes);

		Assert.Equal(ModelRating.Low, ratings.Speed);
		Assert.Equal(ModelRating.High, ratings.Accuracy);
		Assert.Equal(ModelRating.High, ratings.Memory);
	}
}
