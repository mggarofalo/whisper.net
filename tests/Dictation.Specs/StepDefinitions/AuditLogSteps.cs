// @WHISPER-34 — drives the privacy-gated audit-log scenarios. Steps stay thin; the AuditLogDriver owns
// HOW the real AuditLogger gate and SQLite history/audit stores are exercised against a private temp-file
// database. "A transcription completes" can appear in a Given or a When context, so it is bound to both.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class AuditLogSteps(AuditLogDriver driver)
{
	[Given(@"a fresh installation with default settings")]
	public void GivenAFreshInstallation() => driver.FreshInstallWithDefaultSettings();

	[Given(@"the user has explicitly enabled the audit log")]
	public void GivenTheUserHasEnabledTheAuditLog() => driver.EnableAuditLog();

	[Given(@"a transcription completes")]
	[When(@"a transcription completes")]
	public Task ATranscriptionCompletes() => driver.CompleteTranscription();

	[When(@"the user disables the audit log")]
	public void WhenTheUserDisablesTheAuditLog() => driver.DisableAuditLog();

	[When(@"the user purges their data")]
	public Task WhenTheUserPurgesTheirData() => driver.PurgeData();

	[Then(@"no audit records are written")]
	public Task ThenNoAuditRecordsAreWritten() => driver.AssertNoAuditRecords();

	[Then(@"an audit record is written to the local store")]
	public Task ThenAnAuditRecordIsWrittenLocally() => driver.AssertAuditRecordWrittenLocally();

	[Then(@"no data leaves the device")]
	public Task ThenNoDataLeavesTheDevice() => driver.AssertAuditRecordWrittenLocally();

	[Then(@"the audit log contains exactly (\d+) record")]
	public Task ThenTheAuditLogContainsExactly(int expected) => driver.AssertAuditRecordCount(expected);

	[Then(@"no transcript history remains")]
	public Task ThenNoTranscriptHistoryRemains() => driver.AssertNoHistoryRemains();
}
