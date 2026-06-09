// The Whisper.net adapter for the ITranscriber port. It owns the device-INDEPENDENT orchestration:
// pick the compute backend (via the GPU contact point), guard against a missing model file with a
// typed error, lazily load the engine once (the model is an expensive resource), stream the model's
// segments, and fold them into a TranscriptionResult carrying the joined text plus per-segment
// timing/confidence. The native Whisper.net calls live entirely behind IWhisperEngineFactory /
// IWhisperEngine, so this class — and its tests — need no real model. Cancellation is honored
// throughout; model loading reads a local file only and performs no network I/O.

using System.Text;
using Application.Ports;
using Domain.Audio;
using Domain.Models;
using Domain.Settings;
using Logic.ModelManagement;
using Microsoft.Extensions.Options;

namespace Infrastructure.Transcription;

public sealed class WhisperTranscriber(
	IWhisperEngineFactory engineFactory,
	IBackendSelector backendSelector,
	VocabularyConditioner vocabularyConditioner,
	ISettingsStore settingsStore,
	IModelCatalog catalog,
	IModelCache cache,
	IOptions<WhisperOptions> options) : ITranscriber, IAsyncDisposable
{
	private readonly WhisperOptions _options = options.Value;
	private readonly SemaphoreSlim _loadGate = new(1, 1);
	private IWhisperEngine? _engine;
	private string? _loadedModelPath;

	public async ValueTask<TranscriptionResult> TranscribeAsync(AudioClip clip, CancellationToken cancellationToken)
	{
		IWhisperEngine engine = await EnsureEngineLoadedAsync(cancellationToken).ConfigureAwait(false);

		// Assemble decoder conditioning from the CURRENT custom vocabulary on every call, so an edited
		// vocabulary biases the next utterance without reloading the (expensive) model.
		DecodingOptions decodingOptions = vocabularyConditioner.Assemble(_options.CustomVocabulary);

		StringBuilder text = new();
		List<TranscriptionSegment> segments = [];

		await foreach (WhisperSegment segment in engine
			.TranscribeAsync(clip.Samples, clip.SampleRate, decodingOptions, cancellationToken)
			.ConfigureAwait(false))
		{
			text.Append(segment.Text);
			segments.Add(new TranscriptionSegment(segment.Text, segment.Start, segment.End, segment.Probability));
		}

		return new TranscriptionResult(text.ToString().Trim(), segments);
	}

	// Loads the ACTIVE model on first use and caches the engine; reloads when the active model changes. The
	// gate makes a concurrent first call wait for the single load rather than racing two factory loads.
	private async ValueTask<IWhisperEngine> EnsureEngineLoadedAsync(CancellationToken cancellationToken)
	{
		string modelPath = await ResolveModelPathAsync(cancellationToken).ConfigureAwait(false);

		if (_engine is not null && string.Equals(_loadedModelPath, modelPath, StringComparison.OrdinalIgnoreCase))
		{
			return _engine;
		}

		await _loadGate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (_engine is not null && string.Equals(_loadedModelPath, modelPath, StringComparison.OrdinalIgnoreCase))
			{
				return _engine;
			}

			if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath))
			{
				throw new ModelNotFoundException(modelPath);
			}

			// The active model changed (e.g. the user switched models): release the old engine before loading.
			if (_engine is not null)
			{
				await _engine.DisposeAsync().ConfigureAwait(false);
				_engine = null;
			}

			BackendSelection backend = await backendSelector.SelectBackendAsync(cancellationToken).ConfigureAwait(false);
			_engine = engineFactory.Create(modelPath, backend.Backend, _options.Language);
			_loadedModelPath = modelPath;
			return _engine;
		}
		finally
		{
			_loadGate.Release();
		}
	}

	// Resolves the model file to load. An explicit WhisperOptions.ModelPath (config override) wins; otherwise
	// the ACTIVE model from settings is resolved through the catalog + cache — the same resolution the doctor's
	// model check uses — so the model the user downloaded and selected is the one transcription loads
	// (WHISPER-87). Returns an empty path when there is no active/known model, surfaced as ModelNotFound above.
	private async ValueTask<string> ResolveModelPathAsync(CancellationToken cancellationToken)
	{
		if (!string.IsNullOrWhiteSpace(_options.ModelPath))
		{
			return _options.ModelPath;
		}

		AppSettings settings = await settingsStore.LoadAsync(cancellationToken).ConfigureAwait(false);
		WhisperModelCatalogEntry? entry = catalog.Find(settings.ModelId);
		return entry is null ? string.Empty : cache.GetCachedPath(entry);
	}

	public async ValueTask DisposeAsync()
	{
		if (_engine is not null)
		{
			await _engine.DisposeAsync().ConfigureAwait(false);
			_engine = null;
		}

		_loadGate.Dispose();
	}
}
