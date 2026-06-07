// Drives the @WHISPER-17 retention + paged-browsing scenarios. Per AC, these run "against a temp SQLite
// DB", so the driver builds its OWN composition — the REAL Application pipeline (Mediator + validation +
// handlers + Mapperly) over the REAL SqliteHistoryStore pointed at a private temp-file database — rather
// than the spec container's faked IHistoryStore. Recording goes through RecordTranscriptionCommand (which
// enforces retention after each write); browsing goes through BrowseHistoryQuery (validated, then paged).

using Application.Configuration;
using Application.DependencyInjection;
using Application.History;
using Application.Ports;
using AwesomeAssertions;
using Domain.History;
using FluentValidation;
using Infrastructure.Persistence;
using Mediator;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Specs.Drivers;

public sealed class HistoryRetentionDriver : IDisposable
{
	// 1-based minute offsets from this base give each seeded entry a distinct, increasing timestamp, so the
	// oldest/newest are unambiguous.
	private static readonly DateTimeOffset Base = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

	private readonly string _directory = Path.Combine(Path.GetTempPath(), $"whisper-retention-{Guid.NewGuid():N}");

	private int _maxEntries = 1000;
	private string _oldestSeededText = string.Empty;
	private ServiceProvider? _provider;
	private IServiceScope? _scope;

	private IReadOnlyList<TranscriptEntryDto>? _page;
	private Exception? _caught;

	private string DatabasePath => Path.Combine(_directory, "whisper.db");

	private IMediator Mediator
	{
		get
		{
			if (_provider is null)
			{
				ServiceCollection services = new();
				services.AddLogging();
				services.AddApplication();

				int maxEntries = _maxEntries;
				services.Configure<RetentionOptions>(options => options.MaxEntries = maxEntries);

				Directory.CreateDirectory(_directory);
				services.Configure<SqlitePersistenceOptions>(options => options.DatabasePath = DatabasePath);
				services.AddSingleton<SqliteMigrationRunner>();
				services.AddSingleton<SqliteDatabase>();
				services.AddSingleton<IHistoryStore, SqliteHistoryStore>();

				_provider = services.BuildServiceProvider();
				_scope = _provider.CreateScope();
			}

			return _scope!.ServiceProvider.GetRequiredService<IMediator>();
		}
	}

	private IHistoryStore Store => _scope!.ServiceProvider.GetRequiredService<IHistoryStore>();

	// --- Given ---------------------------------------------------------------------------------------

	public void SetRetentionLimit(int maxEntries) => _maxEntries = maxEntries;

	public async Task SeedEntries(int count)
	{
		for (int i = 1; i <= count; i++)
		{
			string text = $"entry {i:D4}";
			if (i == 1)
			{
				_oldestSeededText = text;
			}

			await Mediator.Send(new RecordTranscriptionCommand(text, Base.AddMinutes(i)));
		}
	}

	// --- When ----------------------------------------------------------------------------------------

	public Task RecordNewTranscription() =>
		Mediator.Send(new RecordTranscriptionCommand("the newest entry", Base.AddMinutes(100_000))).AsTask();

	public async Task BrowsePage(int pageSize, int page)
	{
		try
		{
			_page = await Mediator.Send(new BrowseHistoryQuery(pageSize, page));
		}
		catch (ValidationException ex)
		{
			_caught = ex;
		}
	}

	// --- Then ----------------------------------------------------------------------------------------

	public async Task AssertHistoryCount(int expected)
	{
		IReadOnlyList<TranscriptEntry> entries =
			await Store.GetEntriesAsync(from: null, to: null, limit: null, CancellationToken.None);
		entries.Count.Should().Be(expected);
	}

	public async Task AssertOldestPriorEntryRemoved()
	{
		IReadOnlyList<TranscriptEntry> entries =
			await Store.GetEntriesAsync(from: null, to: null, limit: null, CancellationToken.None);
		entries.Should().NotContain(entry => entry.Text == _oldestSeededText);
	}

	public void AssertReceivedCount(int expected)
	{
		_page.Should().NotBeNull();
		_page!.Count.Should().Be(expected);
	}

	public void AssertMostRecentFirst()
	{
		_page.Should().NotBeNull();
		_page!.Should().BeInDescendingOrder(entry => entry.CreatedAt);
	}

	public void AssertBrowseRejected() =>
		_caught.Should().BeOfType<ValidationException>("invalid paging must be rejected by the validation pipeline");

	public void Dispose()
	{
		_scope?.Dispose();
		_provider?.Dispose();
		SqliteConnection.ClearAllPools();
		if (Directory.Exists(_directory))
		{
			Directory.Delete(_directory, recursive: true);
		}
	}
}
