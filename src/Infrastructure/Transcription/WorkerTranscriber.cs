// Tray-side ITranscriber adapter. It resolves the active local model and decoder conditioning, then
// delegates all model/native work to the supervised child process. No Whisper.net API is invoked here.

using Application.Ports;
using Domain.Audio;
using Domain.Models;
using Domain.Settings;
using Logic.ModelManagement;
using Microsoft.Extensions.Options;

namespace Infrastructure.Transcription;

public sealed class WorkerTranscriber(
	IInferenceWorkerSupervisor supervisor,
	VocabularyConditioner vocabularyConditioner,
	ISettingsStore settingsStore,
	IModelCatalog catalog,
	IModelCache cache,
	IOptions<WhisperOptions> options) : ITranscriber
{
	private static readonly float[] WarmupSamples = new float[1_600];
	private readonly WhisperOptions _options = options.Value;

	public async ValueTask<TranscriptionResult> TranscribeAsync(AudioClip clip, CancellationToken cancellationToken)
	{
		string modelPath = await ResolveModelPathAsync(cancellationToken).ConfigureAwait(false);
		DecodingOptions decodingOptions = vocabularyConditioner.Assemble(_options.CustomVocabulary);
		InferenceWorkerRequest request = new(
			modelPath,
			_options.Language,
			decodingOptions,
			clip.SampleRate,
			clip.Samples as float[] ?? [.. clip.Samples]);
		return await supervisor.TranscribeAsync(request, cancellationToken).ConfigureAwait(false);
	}

	public async ValueTask PreloadAsync(CancellationToken cancellationToken)
	{
		string modelPath = await ResolveModelPathAsync(cancellationToken).ConfigureAwait(false);
		InferenceWorkerRequest request = new(
			modelPath,
			_options.Language,
			DecodingOptions.Default,
			16_000,
			WarmupSamples,
			InferenceOperation.WarmUp);
		await supervisor.TranscribeAsync(request, cancellationToken).ConfigureAwait(false);
	}

	private async ValueTask<string> ResolveModelPathAsync(CancellationToken cancellationToken)
	{
		if (!string.IsNullOrWhiteSpace(_options.ModelPath))
		{
			if (!File.Exists(_options.ModelPath))
			{
				throw new ModelNotFoundException(_options.ModelPath);
			}

			return _options.ModelPath;
		}

		AppSettings settings = await settingsStore.LoadAsync(cancellationToken).ConfigureAwait(false);
		WhisperModelCatalogEntry? entry = catalog.Find(settings.ModelId);
		string path = entry is null ? string.Empty : cache.GetCachedPath(entry);
		if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
		{
			throw new ModelNotFoundException(path);
		}

		return path;
	}
}
