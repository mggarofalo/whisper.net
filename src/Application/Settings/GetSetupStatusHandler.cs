// Handles GetSetupStatusQuery: decides whether the app is configured enough to skip first-run
// setup. It is configured only when the user finished setup AND the selected model is actually present in
// the local cache — so a fresh install (setup not completed) and a completed setup whose model file is gone
// ("no active model") both report not-configured, and the launch flow opens the settings window. Pure
// orchestration over the ports: the settings store, the model catalog (to resolve the selected model id),
// and the model cache (to check it is downloaded).

using Application.Interfaces;
using Application.Ports;
using Domain.Models;
using Domain.Settings;

namespace Application.Settings;

public sealed class GetSetupStatusHandler(ISettingsStore store, IModelCatalog catalog, IModelCache cache)
	: IQueryHandler<GetSetupStatusQuery, SetupStatus>
{
	public async ValueTask<SetupStatus> Handle(GetSetupStatusQuery query, CancellationToken cancellationToken)
	{
		AppSettings settings = await store.LoadAsync(cancellationToken);

		WhisperModelCatalogEntry? selected = catalog.Find(settings.ModelId);
		bool modelReady = selected is not null && cache.IsCached(selected);

		return new SetupStatus(settings.SetupCompleted && modelReady);
	}
}
