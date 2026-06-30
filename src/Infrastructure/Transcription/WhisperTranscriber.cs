// The Whisper.net adapter for the ITranscriber port. It owns the device-INDEPENDENT orchestration:
// pick the compute backend (via the GPU contact point), guard against a missing model file with a
// typed error, lazily load the engine once (the model is an expensive resource), stream the model's
// segments, and fold them into a TranscriptionResult carrying the joined text plus per-segment
// timing/confidence. The native Whisper.net calls live entirely behind IWhisperEngineFactory /
// IWhisperEngine, so this class — and its tests — need no real model. Cancellation is honored
// throughout; model loading reads a local file only and performs no network I/O.
//
// Shutdown safety (WHISPER-137): a warm-up or dictation can be in flight when the host disposes this
// singleton. Disposal cancels in-flight work (so it unwinds as cancellation), then DRAINS the load and
// inference gates before disposing them — and every gate acquire/release is guarded so that, even if a
// teardown races an in-flight op, it surfaces as clean cancellation rather than an ObjectDisposedException
// escaping the warm-up path.

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
	// A tenth of a second of digital silence — enough to force the native init (shader compilation /
	// model upload) during warm-up so the first real utterance pays none of that lazy-init cost.
	private static readonly AudioClip WarmupClip = new(new float[1_600], 16_000);

	private readonly WhisperOptions _options = options.Value;
	private readonly SemaphoreSlim _loadGate = new(1, 1);

	// Serializes INFERENCE on the shared engine. The engine is loaded once and reused, so the
	// startup warm-up (PreloadAsync) and the first real dictation (TranscribeAsync) run on the SAME native
	// context — and whisper_full is not safe to run concurrently there. Without this gate a dictation that
	// lands while the warm-up inference is still in flight races it and comes back empty: the lost first
	// utterance. Holding it makes that dictation wait for the warm-up to finish, then transcribe cleanly.
	// Distinct from _loadGate, which only guards the one-time model LOAD, not the per-call inference.
	private readonly SemaphoreSlim _inferenceGate = new(1, 1);

	// Cancels in-flight load/inference when the transcriber is disposed, so a warm-up racing shutdown
	// unwinds as cancellation instead of touching a half-torn-down engine or a disposed gate.
	private readonly CancellationTokenSource _disposeCts = new();
	private int _disposed;

	private IWhisperEngine? _engine;
	private string? _loadedModelPath;

	public async ValueTask<TranscriptionResult> TranscribeAsync(AudioClip clip, CancellationToken cancellationToken)
	{
		using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCts.Token);
		CancellationToken token = linked.Token;

		IWhisperEngine engine = await EnsureEngineLoadedAsync(token).ConfigureAwait(false);

		// Assemble decoder conditioning from the CURRENT custom vocabulary on every call, so an edited
		// vocabulary biases the next utterance without reloading the (expensive) model.
		DecodingOptions decodingOptions = vocabularyConditioner.Assemble(_options.CustomVocabulary);

		// Serialize against any in-flight warm-up/inference on the shared engine: wait here
		// rather than race whisper_full on the same context, which loses the utterance.
		await AcquireAsync(_inferenceGate, token).ConfigureAwait(false);
		try
		{
			StringBuilder text = new();
			List<TranscriptionSegment> segments = [];

			await foreach (WhisperSegment segment in engine
				.TranscribeAsync(clip.Samples, clip.SampleRate, decodingOptions, token)
				.ConfigureAwait(false))
			{
				text.Append(segment.Text);
				segments.Add(new TranscriptionSegment(segment.Text, segment.Start, segment.End, segment.Probability));
			}

			return new TranscriptionResult(text.ToString().Trim(), segments);
		}
		finally
		{
			Release(_inferenceGate);
		}
	}

	// Warm-up: load the active model now and run one throwaway inference over silence so the
	// FIRST real dictation isn't slowed by the cold load + native init. Reuses the same load path as a real
	// transcription, so a missing/absent model surfaces ModelNotFoundException for the caller (the startup
	// warm-up service) to swallow — warm-up is best-effort and must never crash the host or block startup.
	public async ValueTask PreloadAsync(CancellationToken cancellationToken)
	{
		using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCts.Token);
		CancellationToken token = linked.Token;

		IWhisperEngine engine = await EnsureEngineLoadedAsync(token).ConfigureAwait(false);

		// Hold the inference gate across the warm-up inference so a real dictation that arrives
		// mid warm-up waits for it instead of running whisper_full concurrently on the same context.
		await AcquireAsync(_inferenceGate, token).ConfigureAwait(false);
		try
		{
			// Drain the warm-up inference; its output is discarded. DecodingOptions.Default keeps warm-up
			// independent of the live custom vocabulary, which conditions only real transcriptions.
			await foreach (WhisperSegment _ in engine
				.TranscribeAsync(WarmupClip.Samples, WarmupClip.SampleRate, DecodingOptions.Default, token)
				.ConfigureAwait(false))
			{
				// Warm-up output is intentionally ignored.
			}
		}
		finally
		{
			Release(_inferenceGate);
		}
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

		await AcquireAsync(_loadGate, cancellationToken).ConfigureAwait(false);
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
			Release(_loadGate);
		}
	}

	// Resolves the model file to load. An explicit WhisperOptions.ModelPath (config override) wins; otherwise
	// the ACTIVE model from settings is resolved through the catalog + cache — the same resolution the doctor's
	// model check uses — so the model the user downloaded and selected is the one transcription loads
	//. Returns an empty path when there is no active/known model, surfaced as ModelNotFound above.
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

	// Acquire a gate, mapping a dispose-time teardown to cancellation: if the gate was disposed by a racing
	// DisposeAsync, the caller sees OperationCanceledException (shutting down) rather than ObjectDisposedException.
	private static async ValueTask AcquireAsync(SemaphoreSlim gate, CancellationToken cancellationToken)
	{
		try
		{
			await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (ObjectDisposedException)
		{
			throw new OperationCanceledException("The transcriber was disposed while loading or transcribing.");
		}
	}

	// Release a gate, tolerating a DisposeAsync that tore it down while the operation was in flight: the
	// count no longer matters once the transcriber is being disposed, so a disposed gate is a benign no-op.
	private static void Release(SemaphoreSlim gate)
	{
		try
		{
			gate.Release();
		}
		catch (ObjectDisposedException)
		{
			// Disposed during shutdown; nothing to release.
		}
	}

	// Wait for an in-flight holder to let go of a gate before it is disposed, so the engine is not torn down
	// mid-use; tolerant of a gate already disposed by an earlier drain or a double dispose.
	private static async ValueTask DrainAsync(SemaphoreSlim gate)
	{
		try
		{
			await gate.WaitAsync().ConfigureAwait(false);
		}
		catch (ObjectDisposedException)
		{
			// Already torn down; nothing to drain.
		}
	}

	public async ValueTask DisposeAsync()
	{
		// Idempotent: the host and an `await using` could both dispose; cancelling a disposed CTS would throw.
		if (Interlocked.Exchange(ref _disposed, 1) == 1)
		{
			return;
		}

		// Cancel in-flight load/inference so a warm-up racing shutdown unwinds (its linked token trips),
		// then drain both gates so no in-flight op is mid-Wait/Release and the engine isn't disposed mid-use.
		_disposeCts.Cancel();
		await DrainAsync(_loadGate).ConfigureAwait(false);
		await DrainAsync(_inferenceGate).ConfigureAwait(false);

		if (_engine is not null)
		{
			await _engine.DisposeAsync().ConfigureAwait(false);
			_engine = null;
		}

		_loadGate.Dispose();
		_inferenceGate.Dispose();
		_disposeCts.Dispose();
	}
}
