// File-backed implementation of the ISettingsStore port (WHISPER-43): the only place the user's
// settings touch the filesystem. Settings are persisted as JSON of the Application DTO (the hotkey as
// its canonical chord string), so the on-disk shape stays free of domain construction rules; the
// SettingsMapper converts to/from the validated domain AppSettings.
//
// Recovery is deliberate: a first run with no file yields AppSettings.Default and creates the store, so
// the app always starts from a valid, persisted baseline; a corrupt or unreadable file falls back to
// defaults without crashing and logs the recovery rather than propagating the failure to the host.

using System.Text.Json;
using Application.Ports;
using Application.Settings;
using Domain;
using Domain.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Settings;

public sealed class FileSettingsStore(
	IOptions<SettingsStoreOptions> options,
	SettingsMapper mapper,
	ILogger<FileSettingsStore> logger) : ISettingsStore
{
	private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

	private readonly string _path = options.Value.FilePath;

	public async ValueTask<AppSettings> LoadAsync(CancellationToken cancellationToken)
	{
		if (!File.Exists(_path))
		{
			// First run: persist defaults so the app always starts from a valid, stored baseline.
			AppSettings defaults = AppSettings.Default;
			await SaveAsync(defaults, cancellationToken);
			return defaults;
		}

		try
		{
			await using FileStream stream = File.OpenRead(_path);
			AppSettingsDto? dto = await JsonSerializer.DeserializeAsync<AppSettingsDto>(stream, SerializerOptions, cancellationToken);
			if (dto is null)
			{
				throw new JsonException("The settings file deserialized to null.");
			}

			// ToDomain reconstructs the validated AppSettings; bad values (e.g. an empty model id or an
			// unparseable hotkey) surface as DomainException and are treated as a corrupt store below.
			return mapper.ToDomain(dto);
		}
		catch (Exception ex) when (ex is JsonException or IOException or DomainException)
		{
			logger.LogWarning(ex, "Settings store at {Path} is unreadable or corrupt; recovering with default settings.", _path);
			return AppSettings.Default;
		}
	}

	public async ValueTask SaveAsync(AppSettings settings, CancellationToken cancellationToken)
	{
		string? directory = Path.GetDirectoryName(_path);
		if (!string.IsNullOrEmpty(directory))
		{
			Directory.CreateDirectory(directory);
		}

		AppSettingsDto dto = mapper.ToDto(settings);
		await using FileStream stream = File.Create(_path);
		await JsonSerializer.SerializeAsync(stream, dto, SerializerOptions, cancellationToken);
	}
}
