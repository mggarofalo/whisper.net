// Port for the registry of Whisper models the app supports. The catalog is static, on-device data
// (no network), implemented in Logic.ModelManagement; the rest of the app reads it to list models and
// to resolve an id to its descriptor before checking the cache or requesting a download.

using Domain.Models;

namespace Application.Ports;

public interface IModelCatalog
{
	/// <summary>The supported Whisper model variants, in display order.</summary>
	IReadOnlyList<WhisperModelCatalogEntry> Entries { get; }

	/// <summary>Finds the entry with the given id, or returns null when the id is unknown.</summary>
	WhisperModelCatalogEntry? Find(string id);
}
