// A fake model runtime for the scenarios: it lets the REAL ModelLifecycle policy run with
// no native model. Each handle records whether it was warmed up, disposed, or forced to initialize
// lazily on first transcription, so warmup and switch-release behavior is observable.

using Application.Ports;
using Domain.Audio;
using Domain.Models;

namespace Dictation.Specs.Support;

internal sealed class FakeLifecycleRuntime : IModelRuntime
{
	private readonly List<FakeLifecycleHandle> _handles = [];

	public ValueTask<IModelHandle> LoadAsync(ModelLoadRequest request, CancellationToken cancellationToken)
	{
		FakeLifecycleHandle handle = new(request.ModelId);
		_handles.Add(handle);
		return ValueTask.FromResult<IModelHandle>(handle);
	}

	public FakeLifecycleHandle? LastHandleFor(string modelId) =>
		_handles.LastOrDefault(handle => handle.ModelId == modelId);
}

internal sealed class FakeLifecycleHandle(string modelId) : IModelHandle
{
	public string ModelId { get; } = modelId;
	public bool WarmedUp { get; private set; }
	public bool Disposed { get; private set; }
	public bool InitializedLazily { get; private set; }

	public ValueTask WarmUpAsync(CancellationToken cancellationToken)
	{
		WarmedUp = true;
		return ValueTask.CompletedTask;
	}

	public ValueTask<TranscriptionResult> TranscribeAsync(AudioClip clip, CancellationToken cancellationToken)
	{
		if (!WarmedUp)
		{
			InitializedLazily = true;
		}

		return ValueTask.FromResult(new TranscriptionResult($"[{ModelId}]"));
	}

	public ValueTask DisposeAsync()
	{
		Disposed = true;
		return ValueTask.CompletedTask;
	}
}
