// The model lifecycle policy: the single owner of the loaded Whisper model. It loads, unloads, and
// switches models, always releasing the previous model BEFORE activating the next so two native handles
// never coexist; warms a freshly loaded model up (when enabled) so the first utterance isn't slow;
// applies the configured compute precision and the GPU/CPU backend at load; and serializes every
// operation (load/switch/unload AND transcription) through one gate so a transcription either completes
// against the current model or waits for an in-flight switch — never runs against a half-loaded one.
// Current identity/state is published via Status for the UI. The native work is delegated to
// IModelRuntime; this class is pure policy and is unit-tested with a fake runtime.

using Application.Ports;
using Domain.Audio;
using Domain.Models;
using Microsoft.Extensions.Options;

namespace Logic.ModelManagement;

public sealed class ModelLifecycle(
	IModelRuntime runtime,
	IModelCatalog catalog,
	IModelCache cache,
	IBackendSelector backendSelector,
	IOptions<ModelLifecycleOptions> options) : IModelLifecycle, IAsyncDisposable
{
	private readonly ModelLifecycleOptions _options = options.Value;
	private readonly SemaphoreSlim _gate = new(1, 1);
	private IModelHandle? _current;
	private volatile ModelStatus _status = ModelStatus.Unloaded;

	public ModelStatus Status => _status;

	public async ValueTask LoadAsync(string modelId, CancellationToken cancellationToken)
	{
		await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			await LoadUnderGateAsync(modelId, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			_gate.Release();
		}
	}

	// Switching is a load that first releases the current model — the same operation, named for intent.
	public ValueTask SwitchAsync(string modelId, CancellationToken cancellationToken) =>
		LoadAsync(modelId, cancellationToken);

	public async ValueTask UnloadAsync(CancellationToken cancellationToken)
	{
		await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			await ReleaseCurrentAsync().ConfigureAwait(false);
			_status = ModelStatus.Unloaded;
		}
		finally
		{
			_gate.Release();
		}
	}

	public async ValueTask<TranscriptionResult> TranscribeAsync(AudioClip clip, CancellationToken cancellationToken)
	{
		await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			IModelHandle handle = _current
				?? throw new ModelNotFoundException(_status.ModelId ?? string.Empty);

			return await handle.TranscribeAsync(clip, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			_gate.Release();
		}
	}

	private async ValueTask LoadUnderGateAsync(string modelId, CancellationToken cancellationToken)
	{
		WhisperModelCatalogEntry entry = catalog.Find(modelId) ?? throw new ModelNotFoundException(modelId);

		// Release the previous model BEFORE activating the new one — never hold two native handles.
		await ReleaseCurrentAsync().ConfigureAwait(false);
		_status = new ModelStatus(modelId, ModelState.Loading);

		BackendSelection backend = await backendSelector.SelectBackendAsync(cancellationToken).ConfigureAwait(false);
		ModelLoadRequest request = new(modelId, cache.GetCachedPath(entry), backend.Backend, _options.Precision, _options.Language);

		IModelHandle handle = await runtime.LoadAsync(request, cancellationToken).ConfigureAwait(false);

		if (_options.WarmUp)
		{
			await handle.WarmUpAsync(cancellationToken).ConfigureAwait(false);
		}

		_current = handle;
		_status = new ModelStatus(modelId, ModelState.Ready);
	}

	private async ValueTask ReleaseCurrentAsync()
	{
		if (_current is not null)
		{
			await _current.DisposeAsync().ConfigureAwait(false);
			_current = null;
		}
	}

	public async ValueTask DisposeAsync()
	{
		await ReleaseCurrentAsync().ConfigureAwait(false);
		_gate.Dispose();
	}
}
