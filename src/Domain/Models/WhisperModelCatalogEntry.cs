// One entry in the registry of Whisper models the app can run: a stable id, a human-friendly name,
// the quantization, the expected on-disk file name, its size, and (optionally) an expected SHA-256 for
// integrity verification. As a value object it has no identity — two entries with the same fields are
// equal. The registry of these lives in Logic.ModelManagement; Infrastructure uses the file name to
// locate a cached copy and the size/hash to report progress and verify a download.

namespace Domain.Models;

public sealed record WhisperModelCatalogEntry
{
	public string Id { get; }
	public string DisplayName { get; }
	public string Quantization { get; }
	public string FileName { get; }
	public long SizeBytes { get; }

	/// <summary>Expected SHA-256 (lowercase hex) used to verify a download, or empty when unknown.</summary>
	public string Sha256 { get; }

	public WhisperModelCatalogEntry(string id, string displayName, string quantization, string fileName, long sizeBytes, string sha256 = "")
	{
		if (string.IsNullOrWhiteSpace(id))
		{
			throw new DomainException("Model id must not be empty.");
		}

		if (string.IsNullOrWhiteSpace(displayName))
		{
			throw new DomainException("Model display name must not be empty.");
		}

		if (string.IsNullOrWhiteSpace(fileName))
		{
			throw new DomainException("Model file name must not be empty.");
		}

		if (sizeBytes < 0)
		{
			throw new DomainException("Model size must not be negative.");
		}

		Id = id;
		DisplayName = displayName;
		Quantization = quantization ?? string.Empty;
		FileName = fileName;
		SizeBytes = sizeBytes;
		Sha256 = sha256 ?? string.Empty;
	}
}
