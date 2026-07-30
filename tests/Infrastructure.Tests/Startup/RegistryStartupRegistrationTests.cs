// Inner TDD loop for the registry adapter. To avoid touching the machine's real startup
// list, every test points the adapter at a throwaway HKCU subkey (deleted in Dispose). It proves the
// adapter reads the real registration state, writes the current executable path under HKCU (no
// elevation), and that enable/disable are idempotent — repeated toggles leave a single, correct entry
// with no duplicates or orphans. Windows-only, so the test type is annotated accordingly.

using System.Runtime.Versioning;
using Application.Ports;
using AwesomeAssertions;
using Infrastructure.Startup;
using Microsoft.Extensions.Options;
using Microsoft.Win32;
using Xunit;

namespace Infrastructure.Tests.Startup;

[SupportedOSPlatform("windows")]
public sealed class RegistryStartupRegistrationTests : IDisposable
{
	private readonly string _keyPath = $@"Software\whisper.net-tests\{Guid.NewGuid():N}\Run";
	private readonly IStartupRegistration _registration;

	public RegistryStartupRegistrationTests() =>
		_registration = new RegistryStartupRegistration(
			Options.Create(new StartupRegistrationOptions { RunKeyPath = _keyPath, ValueName = "Whisper" }));

	[Fact]
	public void Reports_disabled_when_no_entry_exists() => _registration.IsEnabled().Should().BeFalse();

	// The test host does not run from a Velopack `…\current\` layout, so the launch target is the running
	// executable. The stub-preference rule itself is pinned in StartupLaunchTargetTests.
	[Fact]
	public void Enabling_registers_the_launch_target_for_this_install()
	{
		_registration.Enable();

		_registration.IsEnabled().Should().BeTrue();
		using RegistryKey? key = Registry.CurrentUser.OpenSubKey(_keyPath);
		string? value = key?.GetValue("Whisper") as string;
		value.Should().Contain(Environment.ProcessPath!, "the login command must name the executable to launch");
	}

	[Fact]
	public void Enabling_twice_is_idempotent()
	{
		_registration.Enable();
		_registration.Enable();

		using RegistryKey? key = Registry.CurrentUser.OpenSubKey(_keyPath);
		key!.GetValueNames().Should().ContainSingle(name => name == "Whisper", "there must be no duplicate entries");
	}

	[Fact]
	public void Disabling_removes_the_entry_and_is_idempotent()
	{
		_registration.Enable();

		_registration.Disable();
		Action disableAgain = () => _registration.Disable();

		_registration.IsEnabled().Should().BeFalse();
		disableAgain.Should().NotThrow("disabling an already-absent entry must be a no-op");
	}

	[Fact]
	public void Reports_no_registered_command_when_no_entry_exists()
	{
		_registration.RegisteredCommand.Should().BeNull();
		_registration.RegisteredTargetExists.Should().BeFalse("nothing is registered, so there is no target");
	}

	[Fact]
	public void Reports_the_registered_command_and_that_its_target_exists()
	{
		_registration.Enable();

		_registration.RegisteredCommand.Should().Be(_registration.ExpectedCommand);
		_registration.RegisteredTargetExists.Should().BeTrue("Enable registers the running executable, which exists");
	}

	// The exact drift that leaves "start at login" reading as on while Windows launches nothing: an entry
	// left behind by an install that has since been removed.
	[Fact]
	public void Reports_a_missing_target_for_an_entry_left_by_a_removed_install()
	{
		using (RegistryKey key = Registry.CurrentUser.CreateSubKey(_keyPath))
		{
			key.SetValue("Whisper", @"""C:\Gone\Whisper.Net\current\Presentation.exe""");
		}

		_registration.IsEnabled().Should().BeTrue("an entry is present, which is why the toggle looked correct");
		_registration.RegisteredTargetExists.Should().BeFalse("the executable it names is gone");
		_registration.RegisteredCommand.Should().NotBe(_registration.ExpectedCommand);
	}

	[Fact]
	public void Expected_command_quotes_the_launch_target()
	{
		_registration.ExpectedCommand.Should().StartWith("\"").And.EndWith("\"",
			"an unquoted path containing spaces would be split into arguments at login");
	}

	public void Dispose()
	{
		// Remove the whole throwaway test tree (…\whisper.net-tests\<guid>).
		string root = _keyPath[.._keyPath.IndexOf(@"\Run", StringComparison.Ordinal)];
		Registry.CurrentUser.DeleteSubKeyTree(root, throwOnMissingSubKey: false);
	}
}
