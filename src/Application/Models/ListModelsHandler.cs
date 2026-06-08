// Handles ListModelsQuery (WHISPER-27): reads the on-device catalog and, for each entry, derives its
// ratings from size, asks the cache whether it is already downloaded, and marks the one the model
// lifecycle reports as active. Pure projection over ports — no network, no I/O of its own — so listing
// models never triggers a download.

using Application.Interfaces;
using Application.Ports;
using Domain.Models;

namespace Application.Models;

public sealed class ListModelsHandler(IModelCatalog catalog, IModelCache cache, IModelLifecycle lifecycle)
	: IQueryHandler<ListModelsQuery, IReadOnlyList<ModelListItemDto>>
{
	public ValueTask<IReadOnlyList<ModelListItemDto>> Handle(ListModelsQuery query, CancellationToken cancellationToken)
	{
		string? activeId = lifecycle.Status.ModelId;

		IReadOnlyList<ModelListItemDto> items = catalog.Entries
			.Select(entry => ToDto(entry, activeId))
			.ToList();

		return ValueTask.FromResult(items);
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
