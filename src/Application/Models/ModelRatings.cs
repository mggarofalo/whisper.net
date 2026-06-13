// The three ratings the model picker shows for a model: how fast it transcribes, how
// accurate it is, and how much memory it needs. A value object — two instances with the same three
// ratings are equal. Derived from the model's size by ModelRatingScale rather than hand-authored, so
// the guidance stays consistent as the catalog grows.

namespace Application.Models;

public sealed record ModelRatings(ModelRating Speed, ModelRating Accuracy, ModelRating Memory);
