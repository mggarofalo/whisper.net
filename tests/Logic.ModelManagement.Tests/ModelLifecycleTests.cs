// Inner TDD loop for the model lifecycle policy, over a fake runtime. Confirms load makes a model
// Ready and observable; warmup runs at load (so the first transcription pays no lazy-init cost) and can
// be turned off; switching releases the previous model before the new one is Ready; unload releases and
// reports Unloaded; the configured precision and selected backend are applied at load; transcription
// without a model fails with a typed error; and a switch waits for an in-flight transcription rather
// than disposing a model out from under it.

using Application.Ports;
using AwesomeAssertions;
using Domain.Audio;
using Domain.Models;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Logic.ModelManagement.Tests;

public sealed class ModelLifecycleTests
{
	private readonly FakeModelRuntime _runtime = new();
	private readonly IModelCatalog _catalog = new WhisperModelCatalog();
	private readonly IModelCache _cache = Substitute.For<IModelCache>();
	private readonly IBackendSelector _backend = Substitute.For<IBackendSelector>();

	public ModelLifecycleTests()
	{
		_cache.GetCachedPath(Arg.Any<WhisperModelCatalogEntry>())
			.Returns(call => $"C:/cache/{call.Arg<WhisperModelCatalogEntry>().FileName}");
		_backend.SelectBackendAsync(Arg.Any<CancellationToken>())
			.Returns(new BackendSelection(ComputeBackend.Cpu, "test"));
	}

	private ModelLifecycle CreateLifecycle(ModelLifecycleOptions? options = null) =>
		new(_runtime, _catalog, _cache, _backend, Options.Create(options ?? new ModelLifecycleOptions()));

	private static AudioClip Clip() => new([0.1f, 0.2f], 16_000);

	[Fact]
	public async Task Loading_makes_the_model_ready_and_observable()
	{
		ModelLifecycle lifecycle = CreateLifecycle();

		await lifecycle.LoadAsync("base", CancellationToken.None);

		lifecycle.Status.Should().Be(new ModelStatus("base", ModelState.Ready));
	}

	[Fact]
	public async Task Warms_up_at_load_when_enabled()
	{
		ModelLifecycle lifecycle = CreateLifecycle(new ModelLifecycleOptions { WarmUp = true });

		await lifecycle.LoadAsync("base", CancellationToken.None);

		_runtime.Handles[0].WarmedUp.Should().BeTrue();
	}

	[Fact]
	public async Task Does_not_warm_up_when_disabled()
	{
		ModelLifecycle lifecycle = CreateLifecycle(new ModelLifecycleOptions { WarmUp = false });

		await lifecycle.LoadAsync("base", CancellationToken.None);

		_runtime.Handles[0].WarmedUp.Should().BeFalse();
	}

	[Fact]
	public async Task Warmup_means_the_first_transcription_pays_no_lazy_initialization()
	{
		ModelLifecycle lifecycle = CreateLifecycle(new ModelLifecycleOptions { WarmUp = true });
		await lifecycle.LoadAsync("base", CancellationToken.None);

		await lifecycle.TranscribeAsync(Clip(), CancellationToken.None);

		_runtime.Handles[0].InitializedLazily.Should().BeFalse();
	}

	[Fact]
	public async Task Switching_releases_the_previous_model_before_the_new_one_is_ready()
	{
		ModelLifecycle lifecycle = CreateLifecycle();
		await lifecycle.LoadAsync("base", CancellationToken.None);
		FakeModelHandle baseHandle = _runtime.Handles[0];

		await lifecycle.SwitchAsync("small", CancellationToken.None);

		baseHandle.Disposed.Should().BeTrue();
		lifecycle.Status.Should().Be(new ModelStatus("small", ModelState.Ready));
		_runtime.Handles.Should().HaveCount(2);
	}

	[Fact]
	public async Task Unloading_releases_the_model_and_reports_unloaded()
	{
		ModelLifecycle lifecycle = CreateLifecycle();
		await lifecycle.LoadAsync("base", CancellationToken.None);
		FakeModelHandle handle = _runtime.Handles[0];

		await lifecycle.UnloadAsync(CancellationToken.None);

		handle.Disposed.Should().BeTrue();
		lifecycle.Status.Should().Be(ModelStatus.Unloaded);
	}

	[Fact]
	public async Task Applies_the_configured_precision_and_selected_backend_at_load()
	{
		_backend.SelectBackendAsync(Arg.Any<CancellationToken>())
			.Returns(new BackendSelection(ComputeBackend.Vulkan, "gpu"));
		ModelLifecycle lifecycle = CreateLifecycle(new ModelLifecycleOptions { Precision = ComputePrecision.Float32 });

		await lifecycle.LoadAsync("base", CancellationToken.None);

		_runtime.Requests[0].Precision.Should().Be(ComputePrecision.Float32);
		_runtime.Requests[0].Backend.Should().Be(ComputeBackend.Vulkan);
	}

	[Fact]
	public async Task Transcribing_without_a_loaded_model_fails_with_a_typed_error()
	{
		ModelLifecycle lifecycle = CreateLifecycle();

		Func<Task> act = async () => await lifecycle.TranscribeAsync(Clip(), CancellationToken.None);

		await act.Should().ThrowAsync<ModelNotFoundException>();
	}

	[Fact]
	public async Task Loading_an_unknown_model_fails_with_a_typed_error()
	{
		ModelLifecycle lifecycle = CreateLifecycle();

		Func<Task> act = async () => await lifecycle.LoadAsync("no-such-model", CancellationToken.None);

		await act.Should().ThrowAsync<ModelNotFoundException>();
	}

	[Fact]
	public async Task A_switch_waits_for_an_in_flight_transcription_rather_than_disposing_mid_use()
	{
		TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
		_runtime.HandleFactory = request =>
			request.ModelId == "base" ? new FakeModelHandle("base", _ => gate.Task) : new FakeModelHandle(request.ModelId);

		ModelLifecycle lifecycle = CreateLifecycle(new ModelLifecycleOptions { WarmUp = false });
		await lifecycle.LoadAsync("base", CancellationToken.None);
		FakeModelHandle baseHandle = _runtime.Handles[0];

		Task transcription = lifecycle.TranscribeAsync(Clip(), CancellationToken.None).AsTask();
		await Task.Delay(50, TestContext.Current.CancellationToken);
		Task switching = lifecycle.SwitchAsync("small", CancellationToken.None).AsTask();
		await Task.Delay(50, TestContext.Current.CancellationToken);

		// The switch must not release the model while a transcription is running against it.
		baseHandle.Disposed.Should().BeFalse();
		switching.IsCompleted.Should().BeFalse();

		gate.SetResult();
		await transcription;
		await switching;

		baseHandle.Disposed.Should().BeTrue();
		lifecycle.Status.Should().Be(new ModelStatus("small", ModelState.Ready));
	}
}
