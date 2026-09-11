// Drives the scenario: the real WorkerTranscriber over a capturing fake worker seam.
// It transcribes, mutates the custom vocabulary, transcribes again, and lets the steps assert that the
// second transcription was conditioned with the new term while the engine was loaded only once — i.e.
// the change took effect without restarting the engine. No model file content, no native library.

using Application.Ports;
using AwesomeAssertions;
using Dictation.Specs.Support;
using Domain.Audio;
using Infrastructure.Transcription;
using Logic.ModelManagement;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class VocabularyTranscriptionDriver : IDisposable
{
	private readonly CapturingTranscriptionWorker _worker = new();
	private readonly WhisperOptions _options;
	private readonly WorkerTranscriber _transcriber;
	private readonly string _modelPath;

	public VocabularyTranscriptionDriver()
	{
		// A real (empty) local file so the adapter's existence guard passes; the fake never reads it.
		_modelPath = Path.GetTempFileName();
		_options = new WhisperOptions { ModelPath = _modelPath, Language = "en" };

		_transcriber = new WorkerTranscriber(_worker, new VocabularyConditioner(),
			Substitute.For<ISettingsStore>(), Substitute.For<IModelCatalog>(), Substitute.For<IModelCache>(),
			Options.Create(_options));
	}

	public void StartWithVocabulary(string term) => _options.CustomVocabulary = [term];

	public void ChangeVocabulary(string term) => _options.CustomVocabulary = [term];

	public async Task Transcribe() =>
		await _transcriber.TranscribeAsync(new AudioClip([0.1f, 0.2f, 0.3f], 16_000), CancellationToken.None);

	public void AssertLastPromptContains(string term)
	{
		_worker.LastRequest.Should().NotBeNull();
		_worker.LastRequest!.DecodingOptions.InitialPrompt.Should().Contain(term);
	}

	public void AssertEngineLoadedOnce()
	{
		_worker.CurrentGeneration.Should().Be(1);
		_worker.CallCount.Should().Be(2);
	}

	public void Dispose()
	{
		if (File.Exists(_modelPath))
		{
			File.Delete(_modelPath);
		}
	}
}
