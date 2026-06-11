// Handles ListModelsQuery (WHISPER-27): reads the on-device catalog and, for each entry, derives its
// ratings from size, asks the cache whether it is already downloaded, and marks the active model. The
// active model is the one the lifecycle currently has loaded; before anything is loaded (e.g. just after
// launch) it falls back to the persisted selection in settings.ModelId (WHISPER-118), so the picker
// agrees with the rest of the app — which reads settings.ModelId (WHISPER-98) — instead of showing
// nothing active until the user re-selects. Projection over ports; listing never triggers a download.

using Application.Interfaces;
using Application.Ports;
using Domain.Models;

namespace Application.Models;

public sealed class ListModelsHandler(IModelCatalog catalog, IModelCache cache, IModelLifecycle lifecycle, ISettingsStore settings)
	: IQueryHandler<ListModelsQuery, IReadOnlyList<ModelListItemDto>>
{
	public async ValueTask<IReadOnlyList<ModelListItemDto>> Handle(ListModelsQuery query, CancellationToken cancellationToken)
	{
		// Prefer the loaded model; before one is loaded, the persisted selection is the active model the
		// next transcription will use, so mark that.
		string? activeId = lifecycle.Status.ModelId;
		if (string.IsNullOrEmpty(activeId))
		{
			activeId = (await settings.LoadAsync(cancellationToken)).ModelId;
		}

		IReadOnlyList<ModelListItemDto> items = catalog.Entries
			.Select(entry => ToDto(entry, activeId))
			.ToList();

		return items;
	}

	private ModelListItemDto ToDto(WhisperModelCatalogEntry entry, string? activeId)
	{
		ModelRatings ratings = ModelRatingScale.From(entry.SizeBytes);
		return new ModelListItemDto(
			entry.Id,
			entry.DisplayName,
			entry.SizeBytes,
			ratings.Speed,
			ratings.Accuracy,
			ratings.Memory,
			IsDownloaded: cache.IsCached(entry),
			IsActive: string.Equals(entry.Id, activeId, StringComparison.OrdinalIgnoreCase));
	}
}
