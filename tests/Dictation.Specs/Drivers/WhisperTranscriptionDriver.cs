// The Driver owns HOW on-device transcription is exercised: it builds the REAL WhisperTranscriber over
// a fake engine seam and a stubbed backend selector, runs a clip through it, and captures either the
// result or a typed error. Like VadDriver constructs the real SileroVad over a fake session, this
// exercises the real adapter logic (model-file guard, segment folding) with no model and no native
// library.

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
	private FakeTranscriptionEngineFactory _factory = new(string.Empty);
	private TranscriptionResult? _result;
	private Exception? _error;

	public void GivenLoadedModelTranscribingTo(string text)
	{
		// A real (empty) local file so the adapter's existence guard passes; the fake never reads it.
		_modelPath = Path.GetTempFileName();
		_factory = new FakeTranscriptionEngineFactory(text);
	}

	public void GivenModelPathThatDoesNotExist()
	{
		_modelPath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.bin");
		_factory = new FakeTranscriptionEngineFactory(string.Empty);
	}

	public async Task Transcribe()
	{
		IBackendSelector backendSelector = Substitute.For<IBackendSelector>();
		backendSelector.SelectBackendAsync(Arg.Any<CancellationToken>())
			.Returns(new BackendSelection(ComputeBackend.Cpu, "test"));

		WhisperOptions options = new() { ModelPath = _modelPath, Language = "en" };
		await using WhisperTranscriber transcriber =
			new(_factory, backendSelector, new VocabularyConditioner(), Options.Create(options));

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

	public void AssertNoNetworkEgress() => _factory.NetworkAccessed.Should().BeFalse();

	public void AssertModelNotFoundError() => _error.Should().BeOfType<ModelNotFoundException>();

	// Reaching the assertion at all — with a controlled, typed exception captured rather than the
	// process having torn down — IS the "does not crash" guarantee.
	public void AssertDidNotCrash() => _error.Should().BeOfType<ModelNotFoundException>();
}
