// Drives the @WHISPER-38 AC4 scenario: the REAL WhisperTranscriber over a capturing fake engine seam.
// It transcribes, mutates the custom vocabulary, transcribes again, and lets the steps assert that the
// second transcription was conditioned with the new term while the engine was loaded only once — i.e.
// the change took effect without restarting the engine. No model file content, no native library.

using Application.Ports;
using AwesomeAssertions;
using Dictation.Specs.Support;
using Domain.Audio;
using Domain.Models;
using Infrastructure.Transcription;
using Logic.ModelManagement;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class VocabularyTranscriptionDriver : IDisposable
{
	private readonly CapturingTranscriptionEngineFactory _factory = new();
	private readonly WhisperOptions _options;
	private readonly WhisperTranscriber _transcriber;
	private readonly string _modelPath;

	public VocabularyTranscriptionDriver()
	{
		// A real (empty) local file so the adapter's existence guard passes; the fake never reads it.
		_modelPath = Path.GetTempFileName();
		_options = new WhisperOptions { ModelPath = _modelPath, Language = "en" };

		IBackendSelector backendSelector = Substitute.For<IBackendSelector>();
		backendSelector.SelectBackendAsync(Arg.Any<CancellationToken>())
			.Returns(new BackendSelection(ComputeBackend.Cpu, "test"));

		_transcriber = new WhisperTranscriber(_factory, backendSelector, new VocabularyConditioner(), Options.Create(_options));
	}

	public void StartWithVocabulary(string term) => _options.CustomVocabulary = [term];

	public void ChangeVocabulary(string term) => _options.CustomVocabulary = [term];

	public async Task Transcribe() =>
		await _transcriber.TranscribeAsync(new AudioClip([0.1f, 0.2f, 0.3f], 16_000), CancellationToken.None);

	public void AssertLastPromptContains(string term)
	{
		_factory.LastDecodingOptions.Should().NotBeNull();
		_factory.LastDecodingOptions!.InitialPrompt.Should().Contain(term);
	}

	public void AssertEngineLoadedOnce() => _factory.CreateCount.Should().Be(1);

	public void Dispose()
	{
		if (File.Exists(_modelPath))
		{
			File.Delete(_modelPath);
		}
	}
}
