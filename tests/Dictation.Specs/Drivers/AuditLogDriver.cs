// Drives the @WHISPER-34 privacy-gated audit-log scenarios. Audit logging is a security/privacy feature,
// so the driver builds its OWN composition — the REAL AuditLogger gate (which reads the live settings
// holder) over the REAL SqliteAuditLog + SqliteHistoryStore against a private temp-file database — and
// purges through the REAL PurgeUserDataCommand via Mediator. A completed transcription always records to
// history and additionally writes an audit record only when the user has opted in. Toggling the setting
// on the shared holder takes effect immediately, modelling the no-restart hot toggle.

using Application.DependencyInjection;
using Application.History;
using Application.Ports;
using Application.Privacy;
using AwesomeAssertions;
using Domain.Audit;
using Domain.Settings;
using Infrastructure.Persistence;
using Logic.AppManagement.Audit;
using Logic.AppManagement.Settings;
using Mediator;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Specs.Drivers;

public sealed class AuditLogDriver : IDisposable
{
	private static readonly DateTimeOffset Base = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

	private readonly string _directory = Path.Combine(Path.GetTempPath(), $"whisper-audit-{Guid.NewGuid():N}");

	private int _events;
	private ServiceProvider? _provider;
	private IServiceScope? _scope;

	private string DatabasePath => Path.Combine(_directory, "whisper.db");

	private IServiceProvider Services
	{
		get
		{
			if (_provider is null)
			{
				ServiceCollection services = new();
				services.AddLogging();
				services.AddApplication();

				services.AddSingleton<SettingsHolder>();
				services.AddSingleton<AuditLogger>();

				Directory.CreateDirectory(_directory);
				services.Configure<SqlitePersistenceOptions>(options => options.DatabasePath = DatabasePath);
				services.AddSingleton<SqliteMigrationRunner>();
				services.AddSingleton<SqliteDatabase>();
				services.AddSingleton<IHistoryStore, SqliteHistoryStore>();
				services.AddSingleton<IAuditLog, SqliteAuditLog>();

				_provider = services.BuildServiceProvider();
				_scope = _provider.CreateScope();
			}

			return _scope!.ServiceProvider;
		}
	}

	private SettingsHolder Holder => Services.GetRequiredService<SettingsHolder>();
	private AuditLogger Auditor => Services.GetRequiredService<AuditLogger>();
	private IAuditLog AuditLog => Services.GetRequiredService<IAuditLog>();
	private IHistoryStore History => Services.GetRequiredService<IHistoryStore>();
	private IMediator Mediator => Services.GetRequiredService<IMediator>();

	private void SetAuditEnabled(bool enabled)
	{
		AppSettings d = AppSettings.Default;
		Holder.Current = new AppSettings(
			d.ModelId, d.Hotkey, d.SilenceThresholdMs, d.FillerWordRemovalEnabled, d.CaptureDeviceId, auditLogEnabled: enabled);
	}

	// --- Given -------------------------------------------------------------------------------------

	public void FreshInstallWithDefaultSettings() => Holder.Current = AppSettings.Default;

	public void EnableAuditLog() => SetAuditEnabled(true);

	public void DisableAuditLog() => SetAuditEnabled(false);

	// --- When --------------------------------------------------------------------------------------

	public async Task CompleteTranscription()
	{
		DateTimeOffset when = Base.AddMinutes(_events++);

		// A completed transcription always lands in history (transcript log); the audit record is written
		// only when the gate says auditing is enabled.
		await Mediator.Send(new RecordTranscriptionCommand("a completed transcription", when, TimeSpan.FromSeconds(3)));
		await Auditor.RecordAsync(
			AuditRecord.Create("TranscriptionCompleted", when, detail: "delivered to the focused field"), CancellationToken.None);
	}

	public Task PurgeData() => Mediator.Send(new PurgeUserDataCommand()).AsTask();

	// --- Then --------------------------------------------------------------------------------------

	public async Task AssertNoAuditRecords() =>
		(await AuditLog.CountAsync(CancellationToken.None)).Should().Be(0);

	public async Task AssertAuditRecordWrittenLocally() =>
		(await AuditLog.CountAsync(CancellationToken.None)).Should().BeGreaterThan(0, "the record must be stored in the local audit log");

	public async Task AssertAuditRecordCount(int expected) =>
		(await AuditLog.CountAsync(CancellationToken.None)).Should().Be(expected);

	public async Task AssertNoHistoryRemains()
	{
		IReadOnlyList<Domain.History.TranscriptEntry> entries =
			await History.GetEntriesAsync(from: null, to: null, limit: null, CancellationToken.None);
		entries.Should().BeEmpty();
	}

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
