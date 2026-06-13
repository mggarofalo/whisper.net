// Inner TDD loop for the SQLite audit log: it appends records, counts them, clears them, and
// reports zero on a fresh database; a write against a corrupt database is logged and swallowed rather than
// throwing (so auditing never blocks the pipeline). Driven against a real temp-file database.

using Application.Ports;
using AwesomeAssertions;
using Domain.Audit;
using Infrastructure.Persistence;
using Infrastructure.Tests.TestSupport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Infrastructure.Tests.Persistence;

public sealed class SqliteAuditLogTests : IDisposable
{
	private static readonly DateTimeOffset When = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

	private readonly SqliteTestDatabase _fixture = new();

	private SqliteAuditLog NewLog(ILogger<SqliteAuditLog>? logger = null) =>
		new(_fixture.NewDatabase(), logger ?? NullLogger<SqliteAuditLog>.Instance);

	[Fact]
	public async Task Reports_zero_on_a_fresh_database()
	{
		(await NewLog().CountAsync(CancellationToken.None)).Should().Be(0);
	}

	[Fact]
	public async Task Appends_and_counts_records()
	{
		IAuditLog log = NewLog();
		await log.AppendAsync(AuditRecord.Create("Started", When), CancellationToken.None);
		await log.AppendAsync(AuditRecord.Create("Completed", When.AddSeconds(1)), CancellationToken.None);

		(await NewLog().CountAsync(CancellationToken.None)).Should().Be(2);
	}

	[Fact]
	public async Task Clear_removes_every_record()
	{
		IAuditLog log = NewLog();
		await log.AppendAsync(AuditRecord.Create("Completed", When), CancellationToken.None);

		await NewLog().ClearAsync(CancellationToken.None);

		(await NewLog().CountAsync(CancellationToken.None)).Should().Be(0);
	}

	[Fact]
	public async Task Append_logs_and_does_not_throw_when_the_database_is_corrupt()
	{
		File.WriteAllText(_fixture.DatabasePath, "this is not a valid sqlite database file");
		RecordingLogger<SqliteAuditLog> logger = new();

		Func<Task> appending = async () =>
			await NewLog(logger).AppendAsync(AuditRecord.Create("Completed", When), CancellationToken.None);

		await appending.Should().NotThrowAsync();
		logger.Entries.Should().Contain(entry => entry.Level == LogLevel.Error);
	}

	public void Dispose() => _fixture.Dispose();
}
