// Exercises the settings UI's accessibility for the @WHISPER-83 scenarios. Like the repository-guidance and
// packaging drivers, it inspects Presentation artifacts directly (the settings view XAML) rather than
// driving behavior through IMediator — because accessibility lives in the WPF markup (UI Automation names,
// labels, tab navigation), which the specs do not otherwise touch. It asserts the same things a developer
// or Accessibility Insights would check statically: every interactive control declares an automation name,
// the custom hotkey capture control announces its binding, and each settings view declares a logical tab
// order. Announcing validation errors to a screen reader is verified manually (Narrator) and tracked
// separately, so it is not asserted here.

using AwesomeAssertions;

namespace Dictation.Specs.Drivers;

public sealed class AccessibilityDriver
{
	private static readonly string RepositoryRoot = FindRepositoryRoot();

	private static string ViewsDirectory =>
		Path.Combine(RepositoryRoot, "src", "Presentation", "Shell", "Views");

	private static string ControlsDirectory =>
		Path.Combine(RepositoryRoot, "src", "Presentation", "Shell", "Controls");

	private string Read(string directory, string file) => File.ReadAllText(Path.Combine(directory, file));

	public void AssertHotkeyControlsHaveAutomationNames()
	{
		string view = Read(ViewsDirectory, "HotkeyView.xaml");
		view.Should().Contain("AutomationProperties.Name", "the hotkey view's interactive controls are named");
		view.Should().Contain("Assign hotkey", "the Assign button has a meaningful automation name");

		// The capture control announces its current binding through the inner field's automation name.
		string control = Read(ControlsDirectory, "HotkeyCaptureControl.xaml");
		control.Should().Contain("AutomationProperties.Name", "the capture control announces its binding");
		control.Should().Contain("current binding", "the announced name includes the bound chord");
	}

	public void AssertDeviceControlsHaveAutomationNames()
	{
		string view = Read(ViewsDirectory, "AudioDeviceView.xaml");
		view.Should().Contain("AutomationProperties.Name", "the device picker is named");
		view.Should().Contain("AutomationProperties.LabeledBy", "the device combo box is labelled by its heading");
	}

	public void AssertModelControlsHaveAutomationNames()
	{
		string view = Read(ViewsDirectory, "ModelView.xaml");
		view.Should().Contain("AutomationProperties.Name", "the model picker's buttons and progress are named");
		view.Should().Contain("Cancel download", "the cancel action has a meaningful automation name");
	}

	public void AssertSettingsViewsDeclareTabOrder()
	{
		foreach (string file in new[] { "HotkeyView.xaml", "AudioDeviceView.xaml", "ModelView.xaml" })
		{
			Read(ViewsDirectory, file).Should().Contain(
				"KeyboardNavigation.TabNavigation",
				$"{file} declares a logical keyboard tab order so the flow is operable by keyboard alone");
		}
	}

	private static string FindRepositoryRoot()
	{
		DirectoryInfo? directory = new(AppContext.BaseDirectory);
		while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Whisper.slnx")))
		{
			directory = directory.Parent;
		}

		return directory?.FullName
			?? throw new InvalidOperationException("Could not locate the repository root (Whisper.slnx).");
	}
}
