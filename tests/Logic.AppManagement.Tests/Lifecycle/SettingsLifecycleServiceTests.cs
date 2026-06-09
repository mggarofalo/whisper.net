// Inner TDD loop for the settings-holder sync (WHISPER-98). The lifecycle service loads settings into
// the shared holder on startup and writes the holder back on shutdown. The gap: nothing updated the
// holder when settings changed mid-session, so a graceful shutdown overwrote the store with the stale
// startup snapshot — silently reverting the model/hotkey/device the user changed. These pin that the
// service now tracks change broadcasts so the value saved on shutdown is the latest one.

using Application.Ports;
using Application.Settings;
using AwesomeAssertions;
using Domain.Settings;
using Logic.AppManagement.Lifecycle;
using Logic.AppManagement.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Logic.AppManagement.Tests.Lifecycle;

public sealed class SettingsLifecycleServiceTests
{
	private readonly ISettingsStore _store = Substitute.For<ISettingsStore>();
	private readonly SettingsHolder _holder = new();
	private readonly SettingsChangeBroadcaster _broadcaster = new();

	private static readonly AppSettings Changed =
		new("large-v3", HotkeyBinding.Parse("Ctrl+Shift+D"), silenceThresholdMs: 750, fillerWordRemovalEnabled: false);

	private SettingsLifecycleService NewService() =>
		new(_store, _holder, _broadcaster, NullLogger<SettingsLifecycleService>.Instance);

	[Fact]
	public async Task A_change_broadcast_after_startup_is_persisted_on_shutdown()
	{
		_store.LoadAsync(Arg.Any<CancellationToken>()).Returns(AppSettings.Default);
		SettingsLifecycleService service = NewService();

		await service.StartAsync(CancellationToken.None);
		_broadcaster.Raise(Changed); // a model/hotkey/device change during the session

		_holder.Current.Should().Be(Changed, "the holder tracks the broadcast change");

		await service.StopAsync(CancellationToken.None);

		// Shutdown saves the latest value, not the stale startup snapshot.
		await _store.Received(1).SaveAsync(Changed, Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Stops_tracking_changes_once_shut_down()
	{
		_store.LoadAsync(Arg.Any<CancellationToken>()).Returns(AppSettings.Default);
		SettingsLifecycleService service = NewService();

		await service.StartAsync(CancellationToken.None);
		await service.StopAsync(CancellationToken.None);

		_broadcaster.Raise(Changed); // arrives after shutdown — must be ignored

		_holder.Current.Should().Be(AppSettings.Default, "the service unsubscribed on shutdown");
	}
}
