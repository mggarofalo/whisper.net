// SQLite-backed implementation of the ISettingsStore port: the user's settings persisted as
// a single JSON document (the Application DTO, hotkey as its canonical chord string) in a one-row settings
// table, so the stored shape stays free of domain construction rules; the SettingsMapper converts to/from
// the validated domain AppSettings. A first run with no stored row yields AppSettings.Default and persists
// it, so the app always starts from a valid, stored baseline; a corrupt or unreadable store falls back to
// defaults without crashing and logs the recovery rather than propagating the failure to the host.
//
// A read is retried briefly before that fallback. Not all read failures mean corruption: at login the
// profile is busy — indexers, sync clients, and backup agents all touch a freshly written file — and a
// transient SQLITE_BUSY/locked error would otherwise drop the whole session onto defaults, presenting the
// user with a seemingly factory-reset app. Corruption is not retried (it will not heal), so only the
// genuinely transient case pays the wait.

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
	// How hard a transient read failure is retried before falling back to defaults. Short enough that a
	// genuinely broken store does not delay startup noticeably, long enough to ride out a file held open by
	// another process at login.
	private const int ReadAttempts = 3;
	private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(150);

	public async ValueTask<AppSettings> LoadAsync(CancellationToken cancellationToken)
	{
		for (int attempt = 1; ; attempt++)
		{
			try
			{
				return await ReadAsync(cancellationToken);
			}
			catch (SqliteException ex) when (attempt < ReadAttempts)
			{
				// Transient by assumption: the store was there but could not be opened or read right now.
				// Wait and try again rather than handing the app defaults it might later persist.
				logger.LogWarning(
					ex,
					"Could not read the settings store (attempt {Attempt} of {Attempts}); retrying.",
					attempt,
					ReadAttempts);
				await Task.Delay(RetryDelay, cancellationToken);
			}
			catch (Exception ex) when (ex is SqliteException or JsonException or DomainException)
			{
				// Out of attempts, or corrupt beyond retrying. Recover so the app still starts; the shutdown
				// save is gated on an observed change, so these defaults cannot overwrite the stored document.
				logger.LogWarning(ex, "Settings store is unreadable or corrupt; recovering with default settings.");
				return AppSettings.Default;
			}
		}
	}

	private async ValueTask<AppSettings> ReadAsync(CancellationToken cancellationToken)
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
		// unparseable hotkey) surface as DomainException and are treated as a corrupt store above.
		return mapper.ToDomain(dto);
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
