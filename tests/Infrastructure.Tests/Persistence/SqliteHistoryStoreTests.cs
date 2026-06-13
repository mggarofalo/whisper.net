// Inner TDD loop for the SQLite history store: it appends entries and reads them back
// newest-first, honors the optional limit and inclusive date range, and returns an empty history on a
// fresh database. Driven against a real temp-file database (schema created on first use by the store).

using Application.Ports;
using AwesomeAssertions;
using Domain.History;
using Infrastructure.Persistence;
using Infrastructure.Tests.TestSupport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Infrastructure.Tests.Persistence;

public sealed class SqliteHistoryStoreTests : IDisposable
{
	private static readonly DateTimeOffset Oldest = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
	private static readonly DateTimeOffset Middle = new(2026, 1, 1, 11, 0, 0, TimeSpan.Zero);
	private static readonly DateTimeOffset Newest = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

	private readonly SqliteTestDatabase _fixture = new();

	private SqliteHistoryStore NewStore(ILogger<SqliteHistoryStore>? logger = null) =>
		new(_fixture.NewDatabase(), logger ?? NullLogger<SqliteHistoryStore>.Instance);

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

	[Fact]
	public async Task Round_trips_the_audio_duration()
	{
		TranscriptEntry entry = TranscriptEntry.Create("take notes", Newest, TimeSpan.FromSeconds(12));

		await NewStore().AddAsync(entry, CancellationToken.None);
		IReadOnlyList<TranscriptEntry> entries =
			await NewStore().GetEntriesAsync(from: null, to: null, limit: null, CancellationToken.None);

		entries.Should().ContainSingle();
		entries[0].AudioDuration.Should().Be(TimeSpan.FromSeconds(12));
	}

	[Fact]
	public async Task Add_logs_and_does_not_throw_when_the_database_is_corrupt()
	{
		// A bad database makes initialization fail; recording must degrade safely so the pipeline is never
		// blocked — the failure is logged rather than thrown.
		File.WriteAllText(_fixture.DatabasePath, "this is not a valid sqlite database file");
		RecordingLogger<SqliteHistoryStore> logger = new();

		Func<Task> recording = async () =>
			await NewStore(logger).AddAsync(TranscriptEntry.Create("notes", Newest), CancellationToken.None);

		await recording.Should().NotThrowAsync();
		logger.Entries.Should().Contain(entry => entry.Level == LogLevel.Error);
	}

	[Fact]
	public async Task Prunes_to_the_most_recent_entries_keeping_the_newest()
	{
		IHistoryStore store = NewStore();
		await store.AddAsync(TranscriptEntry.Create("oldest", Oldest), CancellationToken.None);
		await store.AddAsync(TranscriptEntry.Create("middle", Middle), CancellationToken.None);
		await store.AddAsync(TranscriptEntry.Create("newest", Newest), CancellationToken.None);

		int pruned = await NewStore().PruneToMostRecentAsync(maxEntries: 2, CancellationToken.None);

		pruned.Should().Be(1);
		IReadOnlyList<TranscriptEntry> remaining =
			await NewStore().GetEntriesAsync(from: null, to: null, limit: null, CancellationToken.None);
		remaining.Select(entry => entry.Text).Should().Equal("newest", "middle");
	}

	[Fact]
	public async Task Pruning_is_a_no_op_when_under_the_limit()
	{
		IHistoryStore store = NewStore();
		await store.AddAsync(TranscriptEntry.Create("newest", Newest), CancellationToken.None);

		int pruned = await NewStore().PruneToMostRecentAsync(maxEntries: 10, CancellationToken.None);

		pruned.Should().Be(0);
	}

	[Fact]
	public async Task A_non_positive_limit_disables_pruning()
	{
		IHistoryStore store = NewStore();
		await store.AddAsync(TranscriptEntry.Create("newest", Newest), CancellationToken.None);

		int pruned = await NewStore().PruneToMostRecentAsync(maxEntries: 0, CancellationToken.None);

		pruned.Should().Be(0);
		IReadOnlyList<TranscriptEntry> remaining =
			await NewStore().GetEntriesAsync(from: null, to: null, limit: null, CancellationToken.None);
		remaining.Should().ContainSingle();
	}

	[Fact]
	public async Task Clear_removes_all_entries()
	{
		IHistoryStore store = NewStore();
		await store.AddAsync(TranscriptEntry.Create("newest", Newest), CancellationToken.None);
		await store.AddAsync(TranscriptEntry.Create("oldest", Oldest), CancellationToken.None);

		await NewStore().ClearAsync(CancellationToken.None);

		IReadOnlyList<TranscriptEntry> remaining =
			await NewStore().GetEntriesAsync(from: null, to: null, limit: null, CancellationToken.None);
		remaining.Should().BeEmpty();
	}

	public void Dispose() => _fixture.Dispose();
}
