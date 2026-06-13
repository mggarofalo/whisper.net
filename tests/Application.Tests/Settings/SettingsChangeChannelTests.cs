// Inner TDD loop for the instant-apply channel. These pin both publish modes over a real
// WeakReferenceMessenger: an immediate Publish delivers the committed change synchronously (so a live
// service reconfigures within one message round-trip), and PublishDebounced coalesces a burst of noisy
// free-text commits into a single delivery of the latest value once the quiet window elapses — proven
// deterministically with a ManualTimeProvider, no wall-clock waiting.

using Application.Settings;
using Application.Tests.Support;
using CommunityToolkit.Mvvm.Messaging;
using Domain.Settings;
using Xunit;

namespace Application.Tests.Settings;

public sealed class SettingsChangeChannelTests
{
	private static readonly TimeSpan Window = TimeSpan.FromMilliseconds(300);

	private static AppSettings WithModel(string modelId) =>
		new(modelId, HotkeyBinding.Parse("Ctrl+Shift+D"), silenceThresholdMs: 700, fillerWordRemovalEnabled: false);

	[Fact]
	public void Publish_delivers_the_change_immediately()
	{
		WeakReferenceMessenger messenger = new();
		List<AppSettings> received = [];
		object recipient = new();
		messenger.Register<SettingsChangedMessage>(recipient, (_, message) => received.Add(message.Value));
		using SettingsChangeChannel channel = new(messenger, new ManualTimeProvider(), Window);

		channel.Publish(WithModel("small.en"));

		AppSettings single = Assert.Single(received);
		Assert.Equal("small.en", single.ModelId);
		GC.KeepAlive(recipient);
	}

	[Fact]
	public void Debounced_publish_coalesces_a_burst_into_one_delivery_of_the_latest()
	{
		WeakReferenceMessenger messenger = new();
		ManualTimeProvider time = new();
		List<AppSettings> received = [];
		object recipient = new();
		messenger.Register<SettingsChangedMessage>(recipient, (_, message) => received.Add(message.Value));
		using SettingsChangeChannel channel = new(messenger, time, Window);

		// A noisy free-text burst: three commits well inside the quiet window.
		channel.PublishDebounced(WithModel("a"));
		time.Advance(TimeSpan.FromMilliseconds(100));
		channel.PublishDebounced(WithModel("b"));
		time.Advance(TimeSpan.FromMilliseconds(100));
		channel.PublishDebounced(WithModel("c"));

		Assert.Empty(received); // nothing delivered until the quiet window elapses

		// Quiet for the full window: only the latest staged value is delivered, exactly once.
		time.Advance(Window);

		AppSettings single = Assert.Single(received);
		Assert.Equal("c", single.ModelId);
		GC.KeepAlive(recipient);
	}

	[Fact]
	public void Debounced_publish_delivers_again_after_a_settled_change()
	{
		WeakReferenceMessenger messenger = new();
		ManualTimeProvider time = new();
		List<AppSettings> received = [];
		object recipient = new();
		messenger.Register<SettingsChangedMessage>(recipient, (_, message) => received.Add(message.Value));
		using SettingsChangeChannel channel = new(messenger, time, Window);

		channel.PublishDebounced(WithModel("first"));
		time.Advance(Window);

		channel.PublishDebounced(WithModel("second"));
		time.Advance(Window);

		Assert.Equal(["first", "second"], received.Select(settings => settings.ModelId));
		GC.KeepAlive(recipient);
	}
}
