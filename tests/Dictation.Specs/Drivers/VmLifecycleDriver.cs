// Drives the @WHISPER-94 activation-lifecycle scenarios. It owns HOW the lifecycle is exercised so the
// steps stay one-liners: it navigates the REAL ShellViewModel (real NavigationService + cached feature
// view-models from the scenario scope), publishes a real SettingsChangedMessage over the scenario's
// messenger, and asserts at the view-model boundary that the cached Hotkey section reacts exactly while
// active — subscribed on activate, unsubscribed on deactivate, resubscribed on return.

using Application.Settings;
using AwesomeAssertions;
using CommunityToolkit.Mvvm.Messaging;
using Domain.Settings;
using Logic.AppManagement.Shell;

namespace Dictation.Specs.Drivers;

public sealed class VmLifecycleDriver(ShellViewModel shell, HotkeyViewModel hotkey, IMessenger messenger)
{
	private static readonly string RepositoryRoot = FindRepositoryRoot();

	// What the cached hotkey section displayed just before a publish — "did not react" means the display
	// is still exactly this. (Activation seeds the binding from settings since WHISPER-109, so the
	// inactive section's display is the loaded chord, not null.)
	private string? _hotkeyBeforePublish;

	public void OpenShellOn(string section) => shell.NavigateCommand.Execute(section);

	public void NavigateTo(string section) => shell.NavigateCommand.Execute(section);

	public void PublishSettingsChange(string chord)
	{
		_hotkeyBeforePublish = hotkey.CurrentHotkey;
		messenger.Send(new SettingsChangedMessage(
			new AppSettings("base.en", HotkeyBinding.Parse(chord), silenceThresholdMs: 700, fillerWordRemovalEnabled: false)));
	}

	public void AssertCurrentHotkeyShown(string chord) => hotkey.CurrentHotkey.Should().Be(chord);

	public void AssertHotkeyDidNotReact() =>
		hotkey.CurrentHotkey.Should().Be(_hotkeyBeforePublish,
			"an inactive cached section must receive no callbacks (WHISPER-94 AC1)");

	public void AssertLifecycleDocumented()
	{
		string doc = File.ReadAllText(Path.Combine(RepositoryRoot, "docs", "architecture.md"));

		doc.Should().Contain("View-model activation lifecycle", "the lifecycle rule has its own documented section (WHISPER-94 AC3)");
		doc.Should().Contain("deactivated on navigate-away and disposed only at shell teardown",
			"the documented rule states when cached view-models deactivate and when they dispose");
		doc.Should().Contain("WeakReferenceMessenger", "the messenger standard is recorded");
		doc.Should().Contain("OnActivated", "the subscription hooks are named");
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
