// Boundary projection of one catalog model for the model picker: its identity and size,
// the three ratings (derived by ModelRatingScale), whether it is already downloaded to the local cache,
// and whether it is the currently active model. The ViewModel binds a row per item; downloading and
// switching are driven by the picker's commands.

namespace Application.Models;

public sealed record ModelListItemDto(
	string Id,
	string DisplayName,
	long SizeBytes,
	ModelRating Speed,
	ModelRating Accuracy,
	ModelRating Memory,
	bool IsDownloaded,
	bool IsActive);
