// Drives the @WHISPER-24 stats recording + aggregation scenarios. Per AC, recording must persist so totals
// survive a restart, so the driver builds its OWN composition — the REAL Application pipeline (Mediator +
// handlers + Mapperly) and the REAL Logic aggregator over the REAL SqliteHistoryStore pointed at a private
// temp-file database. Recording goes through RecordTranscriptionCommand (carrying the audio duration);
// reading back goes through GetUsageSummaryQuery. A "restart" rebuilds the composition over the same file,
// so the totals genuinely come from disk rather than in-memory state.

using Application.DependencyInjection;
using Application.History;
using Application.Ports;
using Application.Statistics;
using AwesomeAssertions;
using Infrastructure.Persistence;
using Logic.AppManagement;
using Mediator;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Specs.Drivers;

public sealed class UsageStatsRecordingDriver : IDisposable
{
	private static readonly DateTimeOffset Base = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

	private readonly string _directory = Path.Combine(Path.GetTempPath(), $"whisper-stats-{Guid.NewGuid():N}");

	private int _recorded;
	private ServiceProvider? _provider;
	private IServiceScope? _scope;
	private UsageSummaryDto? _summary;

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
				services.AddSingleton<IUsageStatsCalculator, UsageStatsCalculator>();

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

	// --- Given / When ------------------------------------------------------------------------------

	public Task RecordTranscription(int seconds, int characters)
	{
		// Build text of exactly `characters` characters so the recorded character total is deterministic.
		string text = new('x', characters);
		_summary = null;
		return Mediator.Send(new RecordTranscriptionCommand(text, Base.AddMinutes(_recorded++), TimeSpan.FromSeconds(seconds))).AsTask();
	}

	public void Restart()
	{
		// Drop the composition (but keep the database file) so the next access rebuilds over the same file —
		// whatever is read then comes from disk, modelling a process restart.
		_summary = null;
		_scope?.Dispose();
		_provider?.Dispose();
		_scope = null;
		_provider = null;
	}

	// --- Then --------------------------------------------------------------------------------------

	public async Task AssertTranscriptionCount(int expected)
	{
		await EnsureSummary();
		_summary!.TotalTranscriptions.Should().Be(expected);
	}

	public async Task AssertAudioSeconds(int expected)
	{
		await EnsureSummary();
		_summary!.TotalAudioDuration.Should().Be(TimeSpan.FromSeconds(expected));
	}

	public async Task AssertCharacterCount(int expected)
	{
		await EnsureSummary();
		_summary!.TotalCharacters.Should().Be(expected);
	}

	private async Task EnsureSummary() =>
		_summary ??= await Mediator.Send(new GetUsageSummaryQuery(From: null, To: null));

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
