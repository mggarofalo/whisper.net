// The model in use, as the shared "In use" card shows it (WHISPER-153): name, id, download size, ratings and
// whether it is on this device yet. Home builds one from the model list; the Model page binds the card to
// its live ActiveModel row, which carries the same property names, so one template serves both pages.

using Application.Models;

namespace Logic.AppManagement.Shell;

public sealed record InUseModelSummary(
	string Id,
	string DisplayName,
	string SizeText,
	ModelRating Speed,
	ModelRating Accuracy,
	ModelRating Memory,
	bool IsDownloaded)
{
	public static InUseModelSummary From(ModelListItemDto model) => new(
		model.Id,
		model.DisplayName,
		ModelSizeFormatter.Format(model.SizeBytes),
		model.Speed,
		model.Accuracy,
		model.Memory,
		model.IsDownloaded);
}
