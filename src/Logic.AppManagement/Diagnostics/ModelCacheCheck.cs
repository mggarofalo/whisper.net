// Model diagnostic: reports whether the configured Whisper model is present in the local
// cache, and where. It reads the configured model id from the settings store, resolves it through the
// catalog, and asks the cache port — the same path transcription takes — so the verdict reflects the
// real model the app would load. Fails when the configured id is unknown (a broken setting) or when the
// model is not downloaded yet; passes with the on-disk path when it is cached.

using Application.Diagnostics;
using Application.Ports;
using Domain.Models;
using Domain.Settings;

namespace Logic.AppManagement.Diagnostics;

public sealed class ModelCacheCheck(ISettingsStore settings, IModelCatalog catalog, IModelCache cache) : IDiagnosticCheck
{
	public string Name => "Model";

	public async ValueTask<DiagnosticResult> RunAsync(CancellationToken cancellationToken)
	{
		AppSettings current = await settings.LoadAsync(cancellationToken);
		string modelId = current.ModelId;

		WhisperModelCatalogEntry? entry = catalog.Find(modelId);
		if (entry is null)
		{
			return new DiagnosticResult(Name, DiagnosticStatus.Fail, $"The configured model '{modelId}' is not a known model.");
		}

		if (!cache.IsCached(entry))
		{
			return new DiagnosticResult(Name, DiagnosticStatus.Fail, $"Model '{entry.DisplayName}' is not downloaded (expected at {cache.GetCachedPath(entry)}).");
		}

		return new DiagnosticResult(Name, DiagnosticStatus.Pass, $"Model '{entry.DisplayName}' is present at {cache.GetCachedPath(entry)}.");
	}
}
