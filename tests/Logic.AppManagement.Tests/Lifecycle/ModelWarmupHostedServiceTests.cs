// Inner TDD loop for the startup model warm-up (WHISPER-127). The service preloads the dictation model
// (ITranscriber.PreloadAsync) in the background on startup so the first dictation isn't slowed by the cold
// load, and re-warms when the active model changes. These pin: a warm-up fires on startup; a model change
// re-warms; an unrelated settings change does not; and a warm-up failure is swallowed (never crashes the
// host). The warm-up runs on the thread pool, so the assertions await the recorded call count.

using Application.Ports;
using Application.Settings;
using AwesomeAssertions;
using CommunityToolkit.Mvvm.Messaging;
using Domain.Models;
using Domain.Settings;
using Logic.AppManagement.Lifecycle;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Logic.AppManagement.Tests.Lifecycle;

public sealed class ModelWarmupHostedServiceTests
{
	private readonly ITranscriber _transcriber = Substitute.For<ITranscriber>();
	private readonly ISettingsStore _store = Substitute.For<ISettingsStore>();
	private readonly IMessenger _messenger = new WeakReferenceMessenger();
	private int _preloads;

	public ModelWarmupHostedServiceTests()
	{
		// Active model "base.en" by default (AppSettings.Default), counting each warm-up call.
		_store.LoadAsync(Arg.Any<CancellationToken>()).Returns(AppSettings.Default);
		_transcriber.PreloadAsync(Arg.Any<CancellationToken>())
			.Returns(_ => { Interlocked.Increment(ref _preloads); return ValueTask.CompletedTask; });
	}

	private ModelWarmupHostedService NewService() =>
		new(_transcriber, _store, _messenger, NullLogger<ModelWarmupHostedService>.Instance);

	// The warm-up is fire-and-forget on the thread pool; poll the recorded count up to a generous timeout.
	private async Task<bool> WaitForPreloadsAsync(int expected)
	{
		for (int i = 0; i < 200 && Volatile.Read(ref _preloads) < expected; i++)
		{
			await Task.Delay(10);
		}

		return Volatile.Read(ref _preloads) >= expected;
	}

	private static AppSettings WithModel(string modelId) =>
		new(modelId, AppSettings.Default.Hotkey, AppSettings.Default.SilenceThresholdMs, AppSettings.Default.FillerWordRemovalEnabled);

	[Fact]
	public async Task Warms_the_model_on_startup()
	{
		ModelWarmupHostedService service = NewService();

		await service.StartAsync(CancellationToken.None);

		(await WaitForPreloadsAsync(1)).Should().BeTrue("the model is warmed in the background at startup");
		await service.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task Re_warms_when_the_active_model_changes()
	{
		ModelWarmupHostedService service = NewService();
		await service.StartAsync(CancellationToken.None);
		(await WaitForPreloadsAsync(1)).Should().BeTrue();

		_messenger.Send(new SettingsChangedMessage(WithModel("small.en")));

		(await WaitForPreloadsAsync(2)).Should().BeTrue("switching the active model re-warms the dictation engine");
		await service.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task Does_not_re_warm_for_a_settings_change_that_keeps_the_same_model()
	{
		ModelWarmupHostedService service = NewService();
		await service.StartAsync(CancellationToken.None);
		(await WaitForPreloadsAsync(1)).Should().BeTrue();

		// Same model id ("base.en"), a different unrelated field — must not re-warm.
		_messenger.Send(new SettingsChangedMessage(
			new AppSettings("base.en", HotkeyBinding.Parse("Ctrl+Shift+D"), silenceThresholdMs: 999, fillerWordRemovalEnabled: false)));

		await Task.Delay(100, TestContext.Current.CancellationToken); // give any erroneous warm-up a chance to run
		Volatile.Read(ref _preloads).Should().Be(1);
		await service.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task A_failed_warm_up_is_swallowed_and_never_crashes_startup()
	{
		_transcriber.PreloadAsync(Arg.Any<CancellationToken>())
			.Returns(_ => { Interlocked.Increment(ref _preloads); return new ValueTask(Task.FromException(new ModelNotFoundException("none"))); });
		ModelWarmupHostedService service = NewService();

		Func<Task> start = async () => await service.StartAsync(CancellationToken.None);

		await start.Should().NotThrowAsync("warm-up is best-effort and runs in the background");
		(await WaitForPreloadsAsync(1)).Should().BeTrue("the warm-up was attempted");
		await service.StopAsync(CancellationToken.None);
	}
}
