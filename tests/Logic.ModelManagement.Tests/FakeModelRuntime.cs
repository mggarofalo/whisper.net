// Fakes for driving ModelLifecycle's policy without a real model or native runtime. The runtime records
// every load request and the handles it produced; each handle tracks whether it was warmed up, disposed,
// or forced to initialize lazily on first transcription (so warmup behavior is observable), and can be
// made to block inside TranscribeAsync to exercise the concurrency gate.

using Application.Ports;
using Domain.Audio;
using Domain.Models;

namespace Logic.ModelManagement.Tests;

internal sealed class FakeModelRuntime : IModelRuntime
{
	public List<ModelLoadRequest> Requests { get; } = [];
	public List<FakeModelHandle> Handles { get; } = [];
	public Func<ModelLoadRequest, FakeModelHandle>? HandleFactory { get; set; }

	public ValueTask<IModelHandle> LoadAsync(ModelLoadRequest request, CancellationToken cancellationToken)
	{
		Requests.Add(request);
		FakeModelHandle handle = HandleFactory?.Invoke(request) ?? new FakeModelHandle(request.ModelId);
		Handles.Add(handle);
		return ValueTask.FromResult<IModelHandle>(handle);
	}
}

internal sealed class FakeModelHandle(string modelId, Func<CancellationToken, Task>? onTranscribe = null) : IModelHandle
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

	public async ValueTask<TranscriptionResult> TranscribeAsync(AudioClip clip, CancellationToken cancellationToken)
	{
		// A model that was not warmed up pays its initialization cost on the first transcription.
		if (!WarmedUp)
		{
			InitializedLazily = true;
		}

		if (onTranscribe is not null)
		{
			await onTranscribe(cancellationToken).ConfigureAwait(false);
		}

		return new TranscriptionResult($"[{ModelId}]");
	}

	public ValueTask DisposeAsync()
	{
		Disposed = true;
		return ValueTask.CompletedTask;
	}
}
