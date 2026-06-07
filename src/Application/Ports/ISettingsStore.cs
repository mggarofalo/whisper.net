// Port for loading and persisting the user's settings. Implemented in Infrastructure (layered YAML in
// Module 10); faked in specs so the settings handlers can be driven without touching disk.

using Domain.Settings;

namespace Application.Ports;

/// <summary>
/// Loads and saves the user's <see cref="AppSettings"/>.
/// </summary>
/// <remarks>I/O-bound (persistence). Both methods are async and honor cancellation.</remarks>
public interface ISettingsStore
{
	/// <summary>Loads the current settings, returning defaults on a fresh install.</summary>
	ValueTask<AppSettings> LoadAsync(CancellationToken cancellationToken);

	/// <summary>Persists the given settings, replacing any previously stored values.</summary>
	ValueTask SaveAsync(AppSettings settings, CancellationToken cancellationToken);
}
