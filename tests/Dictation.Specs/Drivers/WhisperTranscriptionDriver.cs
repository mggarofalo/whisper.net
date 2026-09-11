// The Driver owns HOW on-device transcription is exercised: it builds the real tray-side
// WorkerTranscriber over a fake isolated-worker seam and captures either the result or typed error.

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

public sealed class WhisperTranscriptionDriver
{
	private string _modelPath = string.Empty;
	private FakeTranscriptionWorker _worker = new(string.Empty);
	private TranscriptionResult? _result;
	private Exception? _error;

	public void GivenLoadedModelTranscribingTo(string text)
	{
		// A real (empty) local file so the adapter's existence guard passes; the fake never reads it.
		_modelPath = Path.GetTempFileName();
		_worker = new FakeTranscriptionWorker(text);
	}

	public void GivenModelPathThatDoesNotExist()
	{
		_modelPath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.bin");
		_worker = new FakeTranscriptionWorker(string.Empty);
	}

	public async Task Transcribe()
	{
		WhisperOptions options = new() { ModelPath = _modelPath, Language = "en" };
		WorkerTranscriber transcriber =
			new(_worker, new VocabularyConditioner(), Substitute.For<ISettingsStore>(),
				Substitute.For<IModelCatalog>(), Substitute.For<IModelCache>(), Options.Create(options));

		try
		{
			_result = await transcriber.TranscribeAsync(new AudioClip([0.1f, 0.2f, 0.3f], 16_000), CancellationToken.None);
		}
		catch (Exception ex)
		{
			_error = ex;
		}
		finally
		{
			if (File.Exists(_modelPath))
			{
				File.Delete(_modelPath);
			}
		}
	}

	public void AssertRecognizedText(string expected)
	{
		_error.Should().BeNull();
		_result!.Text.Should().Be(expected);
	}

	public void AssertNoNetworkEgress() => _worker.NetworkAccessed.Should().BeFalse();

	public void AssertModelNotFoundError() => _error.Should().BeOfType<ModelNotFoundException>();

	// Reaching the assertion at all — with a controlled, typed exception captured rather than the
	// process having torn down — IS the "does not crash" guarantee.
	public void AssertDidNotCrash() => _error.Should().BeOfType<ModelNotFoundException>();
}
