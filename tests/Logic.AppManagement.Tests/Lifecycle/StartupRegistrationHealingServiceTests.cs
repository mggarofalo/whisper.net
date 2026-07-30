// Inner TDD loop for launch-at-login upkeep. The Run entry is written once, when the user flips the
// toggle, and nothing ever revisited it — so a reinstall to a different root (or an install performed under
// a redirected profile) left the entry naming an executable that no longer exists. Windows then did nothing
// at login while the toggle still read as on, because an entry WAS present. These pin the healing: a stale
// or foreign registration is repointed at this install, a correct one is left alone, and a user who never
// opted in is never opted in by the healing.

using Application.Ports;
using Logic.AppManagement.Lifecycle;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Logic.AppManagement.Tests.Lifecycle;

public sealed class StartupRegistrationHealingServiceTests
{
	private const string ThisInstall = "\"C:\\Users\\u\\AppData\\Local\\Whisper.Net\\Presentation.exe\"";
	private const string OtherInstall = "\"D:\\Old\\Whisper.Net\\current\\Presentation.exe\"";

	private readonly IStartupRegistration _registration = Substitute.For<IStartupRegistration>();

	public StartupRegistrationHealingServiceTests() =>
		_registration.ExpectedCommand.Returns(ThisInstall);

	private StartupRegistrationHealingService NewService() =>
		new(_registration, NullLogger<StartupRegistrationHealingService>.Instance);

	[Fact]
	public async Task Repoints_a_registration_whose_target_no_longer_exists()
	{
		_registration.IsEnabled().Returns(true);
		_registration.RegisteredCommand.Returns(OtherInstall);
		_registration.RegisteredTargetExists.Returns(false);

		await NewService().StartAsync(CancellationToken.None);

		_registration.Received(1).Enable();
	}

	[Fact]
	public async Task Repoints_a_registration_that_names_another_install()
	{
		_registration.IsEnabled().Returns(true);
		_registration.RegisteredCommand.Returns(OtherInstall);
		_registration.RegisteredTargetExists.Returns(true); // the other install is still on disk

		await NewService().StartAsync(CancellationToken.None);

		_registration.Received(1).Enable();
	}

	[Fact]
	public async Task Leaves_a_correct_registration_untouched()
	{
		_registration.IsEnabled().Returns(true);
		_registration.RegisteredCommand.Returns(ThisInstall);
		_registration.RegisteredTargetExists.Returns(true);

		await NewService().StartAsync(CancellationToken.None);

		_registration.DidNotReceive().Enable();
	}

	// Healing must never opt a user in: an absent entry is a choice, not drift.
	[Fact]
	public async Task Does_not_register_when_launch_at_login_is_switched_off()
	{
		_registration.IsEnabled().Returns(false);
		_registration.RegisteredCommand.Returns((string?)null);

		await NewService().StartAsync(CancellationToken.None);

		_registration.DidNotReceive().Enable();
	}

	[Fact]
	public async Task Shutdown_touches_nothing()
	{
		_registration.IsEnabled().Returns(true);
		_registration.RegisteredCommand.Returns(ThisInstall);
		_registration.RegisteredTargetExists.Returns(true);

		StartupRegistrationHealingService service = NewService();
		await service.StartAsync(CancellationToken.None);
		await service.StopAsync(CancellationToken.None);

		_registration.DidNotReceive().Disable();
	}
}
