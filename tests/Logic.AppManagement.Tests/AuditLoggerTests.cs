// Unit tests for the audit-log privacy gate (WHISPER-34): nothing is written when auditing is off (the
// default), a record is appended once the user opts in, and disabling it stops writes immediately — the
// gate reads the live settings holder, so the toggle needs no restart. Uses a substituted IAuditLog and
// the real SettingsHolder.

using Application.Ports;
using Domain.Audit;
using Domain.Settings;
using Logic.AppManagement.Audit;
using Logic.AppManagement.Settings;
using NSubstitute;
using Xunit;

namespace Logic.AppManagement.Tests;

public sealed class AuditLoggerTests
{
	private static readonly DateTimeOffset When = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

	private readonly IAuditLog _auditLog = Substitute.For<IAuditLog>();
	private readonly SettingsHolder _holder = new();

	private AuditLogger NewLogger() => new(_holder, _auditLog);

	private static AppSettings WithAudit(bool enabled)
	{
		AppSettings d = AppSettings.Default;
		return new AppSettings(d.ModelId, d.Hotkey, d.SilenceThresholdMs, d.FillerWordRemovalEnabled, d.CaptureDeviceId, enabled);
	}

	private static AuditRecord Record() => AuditRecord.Create("TranscriptionCompleted", When);

	[Fact]
	public async Task Writes_nothing_when_auditing_is_off_by_default()
	{
		_holder.Current = AppSettings.Default;

		await NewLogger().RecordAsync(Record(), CancellationToken.None);

		await _auditLog.DidNotReceive().AppendAsync(Arg.Any<AuditRecord>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Appends_the_record_once_auditing_is_enabled()
	{
		_holder.Current = WithAudit(true);

		await NewLogger().RecordAsync(Record(), CancellationToken.None);

		await _auditLog.Received(1).AppendAsync(Arg.Any<AuditRecord>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Stops_writing_immediately_when_disabled()
	{
		AuditLogger logger = NewLogger();

		_holder.Current = WithAudit(true);
		await logger.RecordAsync(Record(), CancellationToken.None);

		_holder.Current = WithAudit(false);
		await logger.RecordAsync(Record(), CancellationToken.None);

		// Only the write made while enabled reached the store; the post-disable call was gated out.
		await _auditLog.Received(1).AppendAsync(Arg.Any<AuditRecord>(), Arg.Any<CancellationToken>());
	}
}
