// Inner TDD loop for the WHISPER-3 transcriber adapter, over a fake engine seam (no model, no native
// library). Confirms it joins segment text and carries timing/confidence, raises a typed
// ModelNotFoundException for a missing/empty path, passes the configured language and the selected
// backend down to the engine, loads the model only once across calls, and honors cancellation.

using Application.Ports;
using AwesomeAssertions;
using Domain.Audio;
using Domain.Models;
using Infrastructure.Transcription;
using Logic.ModelManagement;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Infrastructure.Tests.Transcription;

public sealed class WhisperTranscriberTests : IDisposable
{
	private readonly IBackendSelector _backendSelector = Substitute.For<IBackendSelector>();
	private readonly List<string> _tempFiles = [];

	public WhisperTranscriberTests() =>
		_backendSelector.SelectBackendAsync(Arg.Any<CancellationToken>())
			.Returns(new BackendSelection(ComputeBackend.Cpu, "test"));

	private WhisperTranscriber CreateTranscriber(
		FakeWhisperEngineFactory factory,
		string modelPath,
		string language = "en",
		IReadOnlyList<string>? vocabulary = null) =>
		new(
			factory,
			_backendSelector,
			new VocabularyConditioner(),
			Options.Create(new WhisperOptions { ModelPath = modelPath, Language = language, CustomVocabulary = vocabulary ?? [] }));

	private string ExistingModelFile()
	{
		string path = Path.GetTempFileName();
		_tempFiles.Add(path);
		return path;
	}

	private static AudioClip Clip() => new([0.1f, 0.2f, 0.3f], 16_000);

	[Fact]
	public async Task Joins_segment_text_and_carries_timing_and_confidence()
	{
		FakeWhisperEngineFactory factory = new(
			new WhisperSegment("hello ", TimeSpan.Zero, TimeSpan.FromSeconds(1), 0.9f),
			new WhisperSegment("world", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), 0.8f));
		await using WhisperTranscriber transcriber = CreateTranscriber(factory, ExistingModelFile());

		TranscriptionResult result = await transcriber.TranscribeAsync(Clip(), CancellationToken.None);

		result.Text.Should().Be("hello world");
		result.Segments.Should().HaveCount(2);
		result.Segments![1].Confidence.Should().Be(0.8f);
		result.Segments[1].End.Should().Be(TimeSpan.FromSeconds(2));
	}

	[Fact]
	public async Task Raises_a_typed_error_when_the_model_file_is_missing()
	{
		FakeWhisperEngineFactory factory = new();
		await using WhisperTranscriber transcriber =
			CreateTranscriber(factory, Path.Combine(Path.GetTempPath(), $"nope-{Guid.NewGuid():N}.bin"));

		Func<Task> act = async () => await transcriber.TranscribeAsync(Clip(), CancellationToken.None);

		await act.Should().ThrowAsync<ModelNotFoundException>();
		factory.CreateCount.Should().Be(0);
	}

	[Fact]
	public async Task Raises_a_typed_error_when_the_model_path_is_empty()
	{
		FakeWhisperEngineFactory factory = new();
		await using WhisperTranscriber transcriber = CreateTranscriber(factory, modelPath: string.Empty);

		Func<Task> act = async () => await transcriber.TranscribeAsync(Clip(), CancellationToken.None);

		await act.Should().ThrowAsync<ModelNotFoundException>();
	}

	[Fact]
	public async Task Passes_the_configured_language_and_selected_backend_to_the_engine()
	{
		_backendSelector.SelectBackendAsync(Arg.Any<CancellationToken>())
			.Returns(new BackendSelection(ComputeBackend.Vulkan, "gpu"));
		FakeWhisperEngineFactory factory = new(new WhisperSegment("x", TimeSpan.Zero, TimeSpan.Zero, 1f));
		await using WhisperTranscriber transcriber = CreateTranscriber(factory, ExistingModelFile(), language: "es");

		await transcriber.TranscribeAsync(Clip(), CancellationToken.None);

		factory.LastLanguage.Should().Be("es");
		factory.LastBackend.Should().Be(ComputeBackend.Vulkan);
	}

	[Fact]
	public async Task Loads_the_model_only_once_across_transcriptions()
	{
		FakeWhisperEngineFactory factory = new(new WhisperSegment("x", TimeSpan.Zero, TimeSpan.Zero, 1f));
		await using WhisperTranscriber transcriber = CreateTranscriber(factory, ExistingModelFile());

		await transcriber.TranscribeAsync(Clip(), CancellationToken.None);
		await transcriber.TranscribeAsync(Clip(), CancellationToken.None);

		factory.CreateCount.Should().Be(1);
	}

	[Fact]
	public async Task Conditions_the_decoder_with_the_custom_vocabulary_prompt()
	{
		FakeWhisperEngineFactory factory = new(new WhisperSegment("x", TimeSpan.Zero, TimeSpan.Zero, 1f));
		await using WhisperTranscriber transcriber =
			CreateTranscriber(factory, ExistingModelFile(), vocabulary: ["Reqnroll", "Velopack"]);

		await transcriber.TranscribeAsync(Clip(), CancellationToken.None);

		factory.LastDecodingOptions!.InitialPrompt.Should().Contain("Reqnroll").And.Contain("Velopack");
		factory.LastDecodingOptions.DisableFirstTokenLogProbThreshold.Should().BeTrue();
	}

	[Fact]
	public async Task Leaves_decoding_at_defaults_when_no_vocabulary_is_configured()
	{
		FakeWhisperEngineFactory factory = new(new WhisperSegment("x", TimeSpan.Zero, TimeSpan.Zero, 1f));
		await using WhisperTranscriber transcriber = CreateTranscriber(factory, ExistingModelFile());

		await transcriber.TranscribeAsync(Clip(), CancellationToken.None);

		factory.LastDecodingOptions.Should().Be(DecodingOptions.Default);
	}

	[Fact]
	public async Task A_changed_vocabulary_conditions_the_next_transcription_without_reloading_the_engine()
	{
		FakeWhisperEngineFactory factory = new(new WhisperSegment("x", TimeSpan.Zero, TimeSpan.Zero, 1f));
		WhisperOptions options = new() { ModelPath = ExistingModelFile(), Language = "en", CustomVocabulary = ["Reqnroll"] };
		await using WhisperTranscriber transcriber =
			new(factory, _backendSelector, new VocabularyConditioner(), Options.Create(options));

		await transcriber.TranscribeAsync(Clip(), CancellationToken.None);
		factory.LastDecodingOptions!.InitialPrompt.Should().Contain("Reqnroll");

		options.CustomVocabulary = ["Velopack"];
		await transcriber.TranscribeAsync(Clip(), CancellationToken.None);

		factory.LastDecodingOptions!.InitialPrompt.Should().Contain("Velopack");
		factory.CreateCount.Should().Be(1);
	}

	[Fact]
	public async Task Honors_cancellation()
	{
		FakeWhisperEngineFactory factory = new(new WhisperSegment("x", TimeSpan.Zero, TimeSpan.Zero, 1f));
		await using WhisperTranscriber transcriber = CreateTranscriber(factory, ExistingModelFile());

		Func<Task> act = async () => await transcriber.TranscribeAsync(Clip(), new CancellationToken(canceled: true));

		await act.Should().ThrowAsync<OperationCanceledException>();
	}

	public void Dispose()
	{
		foreach (string file in _tempFiles)
		{
			File.Delete(file);
		}
	}
}
