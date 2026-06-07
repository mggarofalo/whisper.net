// Inner TDD loop for the WHISPER-11 SQLite history store: it appends entries and reads them back
// newest-first, honors the optional limit and inclusive date range, and returns an empty history on a
// fresh database. Driven against a real temp-file database (schema created on first use by the store).

using Application.Ports;
using AwesomeAssertions;
using Domain.History;
using Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Infrastructure.Tests.Persistence;

public sealed class SqliteHistoryStoreTests : IDisposable
{
	private static readonly DateTimeOffset Oldest = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
	private static readonly DateTimeOffset Middle = new(2026, 1, 1, 11, 0, 0, TimeSpan.Zero);
	private static readonly DateTimeOffset Newest = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

	private readonly SqliteTestDatabase _fixture = new();

	private SqliteHistoryStore NewStore() =>
		new(_fixture.NewDatabase(), NullLogger<SqliteHistoryStore>.Instance);

	[Fact]
	public async Task Returns_empty_on_a_fresh_database()
	{
		IReadOnlyList<TranscriptEntry> entries =
			await NewStore().GetEntriesAsync(from: null, to: null, limit: null, CancellationToken.None);

		entries.Should().BeEmpty();
	}

	[Fact]
	public async Task Round_trips_an_appended_entry()
	{
		TranscriptEntry entry = TranscriptEntry.Create("take notes", Newest);

		await NewStore().AddAsync(entry, CancellationToken.None);
		IReadOnlyList<TranscriptEntry> entries =
			await NewStore().GetEntriesAsync(from: null, to: null, limit: null, CancellationToken.None);

		entries.Should().ContainSingle();
		entries[0].Id.Should().Be(entry.Id);
		entries[0].Text.Should().Be("take notes");
		entries[0].CreatedAt.Should().Be(Newest);
	}

	[Fact]
	public async Task Returns_entries_newest_first()
	{
		IHistoryStore store = NewStore();
		await store.AddAsync(TranscriptEntry.Create("middle", Middle), CancellationToken.None);
		await store.AddAsync(TranscriptEntry.Create("oldest", Oldest), CancellationToken.None);
		await store.AddAsync(TranscriptEntry.Create("newest", Newest), CancellationToken.None);

		IReadOnlyList<TranscriptEntry> entries =
			await NewStore().GetEntriesAsync(from: null, to: null, limit: null, CancellationToken.None);

		entries.Select(entry => entry.Text).Should().Equal("newest", "middle", "oldest");
	}

	[Fact]
	public async Task Honors_the_limit()
	{
		IHistoryStore store = NewStore();
		await store.AddAsync(TranscriptEntry.Create("middle", Middle), CancellationToken.None);
		await store.AddAsync(TranscriptEntry.Create("oldest", Oldest), CancellationToken.None);
		await store.AddAsync(TranscriptEntry.Create("newest", Newest), CancellationToken.None);

		IReadOnlyList<TranscriptEntry> entries =
			await NewStore().GetEntriesAsync(from: null, to: null, limit: 2, CancellationToken.None);

		entries.Select(entry => entry.Text).Should().Equal("newest", "middle");
	}

	[Fact]
	public async Task Filters_by_inclusive_date_range()
	{
		IHistoryStore store = NewStore();
		await store.AddAsync(TranscriptEntry.Create("middle", Middle), CancellationToken.None);
		await store.AddAsync(TranscriptEntry.Create("oldest", Oldest), CancellationToken.None);
		await store.AddAsync(TranscriptEntry.Create("newest", Newest), CancellationToken.None);

		IReadOnlyList<TranscriptEntry> entries =
			await NewStore().GetEntriesAsync(from: Middle, to: Newest, limit: null, CancellationToken.None);

		entries.Select(entry => entry.Text).Should().Equal("newest", "middle");
	}

	public void Dispose() => _fixture.Dispose();
}
