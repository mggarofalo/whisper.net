// Thin step definitions for the repository-guidance feature (@WHISPER-60). Each step delegates to the
// RepositoryGuidanceDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class RepositoryGuidanceSteps(RepositoryGuidanceDriver driver)
{
	// Scene-setting only: the guidance files exist in the repository under test.
	[Given(@"the repository guidance files")]
	public void GivenTheRepositoryGuidanceFiles()
	{
	}

	[Given(@"the commitlint commit-msg hook is installed")]
	public void GivenTheCommitlintCommitMsgHookIsInstalled() => driver.AssertCommitMsgHookInstalled();

	[When(@"a contributor opens CLAUDE\.md")]
	public void WhenAContributorOpensClaudeMd() => driver.OpenClaudeMd();

	[When(@"a contributor commits with the message ""(.*)""")]
	public void WhenAContributorCommitsWithTheMessage(string message) => driver.Commit(message);

	[Then(@"it points them to AGENTS\.md as the canonical source")]
	public void ThenItPointsThemToAgentsMdAsTheCanonicalSource() => driver.AssertClaudeMdPointsToAgentsMd();

	[Then(@"the commit is rejected")]
	public void ThenTheCommitIsRejected() => driver.AssertCommitRejected();

	[Then(@"the commit is accepted")]
	public void ThenTheCommitIsAccepted() => driver.AssertCommitAccepted();
}
