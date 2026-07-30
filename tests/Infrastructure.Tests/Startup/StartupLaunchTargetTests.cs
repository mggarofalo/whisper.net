// Inner TDD loop for the launch-at-login target rule. The bug it guards: registering the running
// executable pins the Run entry to <root>\current\<app>.exe, a path a Velopack update replaces wholesale —
// so an update could leave the entry naming a path that no longer resolves and the app would silently stop
// launching at login. Preferring the stable stub at <root>\<app>.exe survives updates. Pure, so the rule is
// driven with a supplied file-existence probe instead of a real install.

using AwesomeAssertions;
using Infrastructure.Startup;
using Xunit;

namespace Infrastructure.Tests.Startup;

public sealed class StartupLaunchTargetTests
{
	private const string InstallRoot = @"C:\Users\u\AppData\Local\Whisper.Net";
	private const string Payload = $@"{InstallRoot}\current\Presentation.exe";
	private const string Stub = $@"{InstallRoot}\Presentation.exe";

	[Fact]
	public void Prefers_the_velopack_stub_over_the_versioned_payload()
	{
		string target = StartupLaunchTarget.Resolve(Payload, path => path == Stub);

		target.Should().Be(Stub, "the stub survives an update that replaces the `current` folder");
	}

	[Fact]
	public void Falls_back_to_the_running_executable_when_no_stub_is_present()
	{
		string target = StartupLaunchTarget.Resolve(Payload, _ => false);

		target.Should().Be(Payload);
	}

	// A dev build or xcopy deployment has no `current` folder, so there is no stub to look for.
	[Fact]
	public void Registers_the_running_executable_for_an_unrecognised_layout()
	{
		const string loose = @"C:\tools\whisper\Presentation.exe";

		string target = StartupLaunchTarget.Resolve(loose, _ => true);

		target.Should().Be(loose);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public void Yields_nothing_when_the_process_path_is_unknown(string? processPath)
	{
		StartupLaunchTarget.Resolve(processPath, _ => true).Should().BeEmpty();
	}

	// The folder match is case-insensitive, like the rest of Windows path handling.
	[Fact]
	public void Recognises_the_current_folder_regardless_of_case()
	{
		string target = StartupLaunchTarget.Resolve($@"{InstallRoot}\CURRENT\Presentation.exe", path => path == Stub);

		target.Should().Be(Stub);
	}
}
