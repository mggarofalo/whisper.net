// SQLite-backed implementation of the ISettingsStore port: the user's settings persisted as
// a single JSON document (the Application DTO, hotkey as its canonical chord string) in a one-row settings
// table, so the stored shape stays free of domain construction rules; the SettingsMapper converts to/from
// the validated domain AppSettings. A first run with no stored row yields AppSettings.Default and persists
// it, so the app always starts from a valid, stored baseline; a corrupt or unreadable store falls back to
// defaults without crashing and logs the recovery rather than propagating the failure to the host.

using System.Text.Json;
using Application.Ports;
using Application.Settings;
using Domain;
using Domain.Settings;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence;

public sealed class SqliteSettingsStore(
	SqliteDatabase database,
	SettingsMapper mapper,
	ILogger<SqliteSettingsStore> logger) : ISettingsStore
{
	public async ValueTask<AppSettings> LoadAsync(CancellationToken cancellationToken)
	{
		try
		{
			await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
			await using SqliteCommand command = connection.CreateCommand();
			command.CommandText = "SELECT document FROM settings WHERE id = 0;";

			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			if (!await reader.ReadAsync(cancellationToken))
			{
				// First run: persist defaults so the app always starts from a valid, stored baseline.
				AppSettings defaults = AppSettings.Default;
				await SaveAsync(defaults, cancellationToken);
				return defaults;
			}

			string document = reader.GetString(0);
			AppSettingsDto? dto = JsonSerializer.Deserialize<AppSettingsDto>(document);
			if (dto is null)
			{
				throw new JsonException("The settings document deserialized to null.");
			}

			// ToDomain reconstructs the validated AppSettings; bad values (e.g. an empty model id or an
			// unparseable hotkey) surface as DomainException and are treated as a corrupt store below.
			return mapper.ToDomain(dto);
		}
		catch (Exception ex) when (ex is SqliteException or JsonException or DomainException)
		{
			logger.LogWarning(ex, "Settings store is unreadable or corrupt; recovering with default settings.");
			return AppSettings.Default;
		}
	}

	public async ValueTask SaveAsync(AppSettings settings, CancellationToken cancellationToken)
	{
		try
		{
			AppSettingsDto dto = mapper.ToDto(settings);
			string document = JsonSerializer.Serialize(dto);

			await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
			await using SqliteCommand command = connection.CreateCommand();
			command.CommandText =
				"""
				INSERT INTO settings (id, document) VALUES (0, $document)
				ON CONFLICT (id) DO UPDATE SET document = excluded.document;
				""";
			command.Parameters.AddWithValue("$document", document);
			await command.ExecuteNonQueryAsync(cancellationToken);
		}
		catch (SqliteException ex)
		{
			logger.LogError(ex, "Failed to persist settings to the SQLite store.");
		}
	}
}
