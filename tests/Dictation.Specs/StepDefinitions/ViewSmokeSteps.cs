// Thin step definitions for the @WHISPER-96 view-smoke-harness feature. Each step delegates to the
// ViewSmokeDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class ViewSmokeSteps(ViewSmokeDriver driver)
{
	[Then(@"an sta smoke project constructs each feature view against its view-model")]
	public void ThenAnStaSmokeProjectConstructsEachFeatureView() => driver.AssertStaHarnessConstructsEachFeatureView();

	[Then(@"the smoke harness fails on data-binding errors")]
	public void ThenTheSmokeHarnessFailsOnDataBindingErrors() => driver.AssertHarnessFailsOnBindingErrors();

	[Then(@"the smoke harness checks every registered section for a matching data template")]
	public void ThenTheSmokeHarnessChecksEveryRegisteredSection() => driver.AssertTemplateCompletenessChecked();

	[Then(@"the testing guide records the flaui adopt-versus-defer decision")]
	public void ThenTheTestingGuideRecordsTheFlauiDecision() => driver.AssertFlauiDecisionRecorded();

	[Then(@"the smoke project is part of the solution gated by the windows ci test step")]
	public void ThenTheSmokeProjectIsPartOfTheGatedSolution() => driver.AssertCiRunsSmokeLayerOnWindows();
}
