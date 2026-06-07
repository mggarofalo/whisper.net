// Thin step definitions for the domain-invariant feature. Each step delegates to the
// DomainInvariantsDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class DomainInvariantsSteps(DomainInvariantsDriver driver)
{
	[Given(@"a recording session started at ""(.*)""")]
	public void GivenARecordingSessionStartedAt(string time) => driver.StartSessionAt(time);

	[When(@"the session is ended at ""(.*)""")]
	public void WhenTheSessionIsEndedAt(string time) => driver.EndSessionAt(time);

	[When(@"a transcript entry is created with empty recognized text")]
	public void WhenATranscriptEntryIsCreatedWithEmptyText() => driver.CreateTranscriptWithEmptyText();

	[When(@"usage statistics are created with (-?\d+) words across (-?\d+) sessions")]
	public void WhenUsageStatisticsAreCreated(int words, int sessions) => driver.CreateUsageStats(words, sessions);

	[Then("the domain rejects the operation as an invariant violation")]
	[Then("the domain rejects the entry as an invariant violation")]
	public void ThenTheDomainRejectsAsAnInvariantViolation() => driver.AssertRejectedAsInvariantViolation();

	[Then(@"construction succeeds")]
	public void ThenConstructionSucceeds() => driver.AssertConstructionSucceeded();

	[Then(@"construction is rejected")]
	public void ThenConstructionIsRejected() => driver.AssertRejectedAsInvariantViolation();
}
