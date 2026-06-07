// The Driver owns HOW the model lifecycle is exercised: it builds the REAL ModelLifecycle policy over a
// fake runtime (and stubbed cache/backend), drives load/switch/transcribe, and asserts on the observable
// status and on the fake handles (warmed up, released). Like the other drivers, this runs the real
// policy logic with only the device layer faked.

using Application.Ports;
using AwesomeAssertions;
using Dictation.Specs.Support;
using Domain.Audio;
using Domain.Models;
using Logic.ModelManagement;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class ModelLifecycleDriver : IDisposable
{
	private readonly FakeLifecycleRuntime _runtime = new();
	private readonly IModelCatalog _catalog = new WhisperModelCatalog();
	private ModelLifecycle? _lifecycle;
	private TranscriptionResult? _result;

	private ModelLifecycle Build(bool warmUp)
	{
		IModelCache cache = Substitute.For<IModelCache>();
		cache.GetCachedPath(Arg.Any<WhisperModelCatalogEntry>())
			.Returns(call => $"C:/cache/{call.Arg<WhisperModelCatalogEntry>().FileName}");

		IBackendSelector backend = Substitute.For<IBackendSelector>();
		backend.SelectBackendAsync(Arg.Any<CancellationToken>())
			.Returns(new BackendSelection(ComputeBackend.Cpu, "test"));

		return new ModelLifecycle(_runtime, _catalog, cache, backend, Options.Create(new ModelLifecycleOptions { WarmUp = warmUp }));
	}

	public async Task GivenModelLoadedWithWarmup()
	{
		_lifecycle = Build(warmUp: true);
		await _lifecycle.LoadAsync("base", CancellationToken.None);
	}

	public async Task GivenModelLoadedAndReady(string modelId)
	{
		_lifecycle = Build(warmUp: true);
		await _lifecycle.LoadAsync(modelId, CancellationToken.None);
	}

	public async Task RequestFirstTranscription() =>
		_result = await _lifecycle!.TranscribeAsync(new AudioClip([0.1f, 0.2f], 16_000), CancellationToken.None);

	public Task SwitchTo(string modelId) => _lifecycle!.SwitchAsync(modelId, CancellationToken.None).AsTask();

	public void AssertRanWithoutLazyInitialization()
	{
		_result.Should().NotBeNull();
		_runtime.LastHandleFor("base")!.InitializedLazily.Should().BeFalse();
	}

	public void AssertActiveReadyModelIs(string modelId) =>
		_lifecycle!.Status.Should().Be(new ModelStatus(modelId, ModelState.Ready));

	public void AssertModelReleased(string modelId) =>
		_runtime.LastHandleFor(modelId)!.Disposed.Should().BeTrue();

	// Reqnroll's DI plugin disposes the per-scenario scope synchronously, so the driver is IDisposable.
	// The lifecycle and its fake handles dispose synchronously, so resolving the ValueTask here is safe.
	public void Dispose() => _lifecycle?.DisposeAsync().AsTask().GetAwaiter().GetResult();
}
