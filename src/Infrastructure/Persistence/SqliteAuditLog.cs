// SQLite-backed implementation of the IAuditLog port (WHISPER-34): appends audit records to a local
// table, counts them, and clears them. Local-only by construction — it talks only to the on-device SQLite
// database, never the network. Writes fail safe (logged and swallowed) so an audit hiccup never blocks
// the pipeline; a count failure reports zero rather than crashing the host. The decision of WHETHER to
// write lives in the Logic gate (AuditLogger); this adapter only persists what it is handed.

using System.Globalization;
using Application.Ports;
using Domain.Audit;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence;

public sealed class SqliteAuditLog(SqliteDatabase database, ILogger<SqliteAuditLog> logger) : IAuditLog
{
	public async ValueTask AppendAsync(AuditRecord record, CancellationToken cancellationToken)
	{
		try
		{
			await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
			await using SqliteCommand command = connection.CreateCommand();
			command.CommandText =
				"""
				INSERT INTO audit_log (id, occurred_at, occurred_ticks, event, detail)
				VALUES ($id, $occurred_at, $occurred_ticks, $event, $detail)
				ON CONFLICT (id) DO NOTHING;
				""";
			command.Parameters.AddWithValue("$id", record.Id.ToString());
			command.Parameters.AddWithValue("$occurred_at", record.OccurredAt.ToString("O", CultureInfo.InvariantCulture));
			command.Parameters.AddWithValue("$occurred_ticks", record.OccurredAt.UtcTicks);
			command.Parameters.AddWithValue("$event", record.Event);
			command.Parameters.AddWithValue("$detail", record.Detail);
			await command.ExecuteNonQueryAsync(cancellationToken);
		}
		catch (SqliteException ex)
		{
			logger.LogError(ex, "Failed to append audit record {RecordId} to the audit log.", record.Id);
		}
	}

	public async ValueTask<int> CountAsync(CancellationToken cancellationToken)
	{
		try
		{
			await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
			await using SqliteCommand command = connection.CreateCommand();
			command.CommandText = "SELECT COUNT(*) FROM audit_log;";
			object? result = await command.ExecuteScalarAsync(cancellationToken);
			return Convert.ToInt32(result, CultureInfo.InvariantCulture);
		}
		catch (SqliteException ex)
		{
			logger.LogError(ex, "Failed to count audit records; reporting zero.");
			return 0;
		}
	}

	public async ValueTask ClearAsync(CancellationToken cancellationToken)
	{
		try
		{
			await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
			await using SqliteCommand command = connection.CreateCommand();
			command.CommandText = "DELETE FROM audit_log;";
			await command.ExecuteNonQueryAsync(cancellationToken);
		}
		catch (SqliteException ex)
		{
			logger.LogError(ex, "Failed to clear the audit log.");
		}
	}
}
