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

	[Fact]
	public void Enabling_registers_the_current_executable_path()
	{
		_registration.Enable();

		_registration.IsEnabled().Should().BeTrue();
		using RegistryKey? key = Registry.CurrentUser.OpenSubKey(_keyPath);
		string? value = key?.GetValue("Whisper") as string;
		value.Should().Contain(Environment.ProcessPath!, "the login command must be the current executable path");
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

	public void Dispose()
	{
		// Remove the whole throwaway test tree (…\whisper.net-tests\<guid>).
		string root = _keyPath[.._keyPath.IndexOf(@"\Run", StringComparison.Ordinal)];
		Registry.CurrentUser.DeleteSubKeyTree(root, throwOnMissingSubKey: false);
	}
}
