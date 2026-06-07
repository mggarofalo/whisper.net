// Acquires a missing model into the local cache. It streams the bytes (from the network source) to a
// temporary ".part" file beside the cache target, reporting byte/percent progress as it goes; verifies
// the result (non-empty, and SHA-256 when the catalog supplies an expected hash); then atomically moves
// the verified file into place. If anything fails — including cancellation — the temp file is deleted,
// so the cache never holds a partial or corrupt model. The cache file appears only once it is complete
// and verified. This runs only when the user requests a download; nothing here is invoked automatically.

using System.Security.Cryptography;
using Application.Ports;
using Domain.Models;

namespace Infrastructure.Models;

public sealed class ModelDownloader(IModelDownloadSource source, IModelCache cache) : IModelDownloader
{
	public async ValueTask<string> DownloadAsync(
		WhisperModelCatalogEntry entry,
		IProgress<ModelDownloadProgress>? progress,
		CancellationToken cancellationToken)
	{
		string finalPath = cache.GetCachedPath(entry);
		string? directory = Path.GetDirectoryName(finalPath);
		if (!string.IsNullOrEmpty(directory))
		{
			Directory.CreateDirectory(directory);
		}

		string tempPath = finalPath + ".part";

		try
		{
			await using ModelDownload download = await source.OpenAsync(entry, cancellationToken).ConfigureAwait(false);
			long? total = download.TotalBytes ?? (entry.SizeBytes > 0 ? entry.SizeBytes : null);

			using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
			long downloaded = 0;
			byte[] buffer = new byte[81_920];

			await using (FileStream file = new(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
			{
				int read;
				while ((read = await download.Content.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
				{
					await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
					hash.AppendData(buffer, 0, read);
					downloaded += read;
					progress?.Report(new ModelDownloadProgress(downloaded, total));
				}
			}

			VerifyIntegrity(entry, tempPath, hash);
			File.Move(tempPath, finalPath, overwrite: true);
			return finalPath;
		}
		catch
		{
			TryDelete(tempPath);
			throw;
		}
	}

	// A download is "verified" when it is non-empty and, where the catalog provides an expected SHA-256,
	// matches it. Without an expected hash the non-empty check is the available guard.
	private static void VerifyIntegrity(WhisperModelCatalogEntry entry, string tempPath, IncrementalHash hash)
	{
		if (new FileInfo(tempPath).Length == 0)
		{
			throw new ModelLoadException(entry.FileName, new InvalidDataException("The downloaded model file was empty."));
		}

		if (!string.IsNullOrEmpty(entry.Sha256))
		{
			string actual = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
			if (!string.Equals(actual, entry.Sha256, StringComparison.OrdinalIgnoreCase))
			{
				throw new ModelLoadException(
					entry.FileName,
					new InvalidDataException($"SHA-256 mismatch for '{entry.FileName}': expected {entry.Sha256}, got {actual}."));
			}
		}
	}

	private static void TryDelete(string path)
	{
		try
		{
			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}
		catch (IOException)
		{
			// Best effort: a leftover ".part" is harmless and will be overwritten on the next attempt.
		}
		catch (UnauthorizedAccessException)
		{
		}
	}
}
