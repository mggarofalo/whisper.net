// SQLite-backed implementation of the IHistoryStore port: appends completed transcriptions
// and reads them back newest-first, optionally bounded by an inclusive date range and/or a maximum count.
// Each entry's timestamp is stored twice — the original DateTimeOffset as a round-trip ("O") string for
// lossless reload, and its UtcTicks as an integer for correct chronological ordering and range filtering
// independent of the entry's offset. Reads fail safe to an empty history (logged) and a write failure is
// logged and swallowed, so a persistence hiccup degrades gracefully rather than crashing the host or
// breaking the transcription pipeline.

using System.Globalization;
using Application.Ports;
using Domain.History;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence;

public sealed class SqliteHistoryStore(SqliteDatabase database, ILogger<SqliteHistoryStore> logger) : IHistoryStore
{
	public async ValueTask AddAsync(TranscriptEntry entry, CancellationToken cancellationToken)
	{
		try
		{
			await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
			await using SqliteCommand command = connection.CreateCommand();
			command.CommandText =
				"""
				INSERT INTO history (id, text, created_at, created_ticks, word_count, duration_ticks)
				VALUES ($id, $text, $created_at, $created_ticks, $word_count, $duration_ticks)
				ON CONFLICT (id) DO NOTHING;
				""";
			command.Parameters.AddWithValue("$id", entry.Id.ToString());
			command.Parameters.AddWithValue("$text", entry.Text);
			command.Parameters.AddWithValue("$created_at", entry.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
			command.Parameters.AddWithValue("$created_ticks", entry.CreatedAt.UtcTicks);
			command.Parameters.AddWithValue("$word_count", entry.WordCount);
			command.Parameters.AddWithValue("$duration_ticks", entry.AudioDuration.Ticks);
			await command.ExecuteNonQueryAsync(cancellationToken);
		}
		catch (SqliteException ex)
		{
			logger.LogError(ex, "Failed to append transcript entry {EntryId} to the history store.", entry.Id);
		}
	}

	public async ValueTask<int> PruneToMostRecentAsync(int maxEntries, CancellationToken cancellationToken)
	{
		// A non-positive limit means "keep everything" — pruning is disabled rather than wiping the table.
		if (maxEntries <= 0)
		{
			return 0;
		}

		try
		{
			await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
			await using SqliteCommand command = connection.CreateCommand();
			command.CommandText =
				"""
				DELETE FROM history
				WHERE id NOT IN (
					SELECT id FROM history ORDER BY created_ticks DESC LIMIT $max
				);
				""";
			command.Parameters.AddWithValue("$max", maxEntries);
			return await command.ExecuteNonQueryAsync(cancellationToken);
		}
		catch (SqliteException ex)
		{
			logger.LogError(ex, "Failed to prune transcript history to the most recent {MaxEntries} entries.", maxEntries);
			return 0;
		}
	}

	public async ValueTask ClearAsync(CancellationToken cancellationToken)
	{
		try
		{
			await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
			await using SqliteCommand command = connection.CreateCommand();
			command.CommandText = "DELETE FROM history;";
			await command.ExecuteNonQueryAsync(cancellationToken);
		}
		catch (SqliteException ex)
		{
			logger.LogError(ex, "Failed to clear transcript history.");
		}
	}

	public async ValueTask<IReadOnlyList<TranscriptEntry>> GetEntriesAsync(
		DateTimeOffset? from,
		DateTimeOffset? to,
		int? limit,
		CancellationToken cancellationToken)
	{
		List<TranscriptEntry> entries = [];

		try
		{
			await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
			await using SqliteCommand command = connection.CreateCommand();
			command.CommandText =
				"""
				SELECT id, text, created_at, duration_ticks
				FROM history
				WHERE ($from IS NULL OR created_ticks >= $from)
				  AND ($to   IS NULL OR created_ticks <= $to)
				ORDER BY created_ticks DESC
				LIMIT $limit;
				""";
			command.Parameters.AddWithValue("$from", from?.UtcTicks ?? (object)DBNull.Value);
			command.Parameters.AddWithValue("$to", to?.UtcTicks ?? (object)DBNull.Value);

			// A null or non-positive limit means "no limit": SQLite treats a negative LIMIT as unbounded.
			command.Parameters.AddWithValue("$limit", limit is > 0 ? limit.Value : -1);

			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
			{
				Guid id = Guid.Parse(reader.GetString(0));
				string text = reader.GetString(1);
				DateTimeOffset createdAt = DateTimeOffset.Parse(
					reader.GetString(2), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
				TimeSpan audioDuration = TimeSpan.FromTicks(reader.GetInt64(3));

				entries.Add(new TranscriptEntry(id, text, createdAt, audioDuration));
			}
		}
		catch (SqliteException ex)
		{
			logger.LogError(ex, "Failed to read transcript history; returning an empty result.");
			return [];
		}

		return entries;
	}
}
