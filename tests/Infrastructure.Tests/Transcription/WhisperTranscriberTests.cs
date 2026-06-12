// Inner TDD loop for the WHISPER-3 transcriber adapter, over a fake engine seam (no model, no native
// library). Confirms it joins segment text and carries timing/confidence, raises a typed
// ModelNotFoundException for a missing/empty path, passes the configured language and the selected
// backend down to the engine, loads the model only once across calls, and honors cancellation.

using Application.Ports;
using AwesomeAssertions;
using Domain.Audio;
using Domain.Models;
using Domain.Settings;
using Infrastructure.Transcription;
using Logic.ModelManagement;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Infrastructure.Tests.Transcription;

public sealed class WhisperTranscriberTests : IDisposable
{
	private readonly IBackendSelector _backendSelector = Substitute.For<IBackendSelector>();
	private readonly ISettingsStore _settings = Substitute.For<ISettingsStore>();
	private readonly IModelCatalog _catalog = Substitute.For<IModelCatalog>();
	private readonly IModelCache _cache = Substitute.For<IModelCache>();
	private readonly List<string> _tempFiles = [];

	public WhisperTranscriberTests()
	{
		_backendSelector.SelectBackendAsync(Arg.Any<CancellationToken>())
			.Returns(new BackendSelection(ComputeBackend.Cpu, "test"));

		// Default: a valid (but unresolved) active model, so the override-path tests don't touch the
		// active-model resolution and the "no active model" path yields an empty path (ModelNotFound).
		_settings.LoadAsync(Arg.Any<CancellationToken>()).Returns(AppSettings.Default);
	}

	private WhisperTranscriber CreateTranscriber(
		FakeWhisperEngineFactory factory,
		string modelPath,
		string language = "en",
		IReadOnlyList<string>? vocabulary = null) =>
		new(
			factory,
			_backendSelector,
			new VocabularyConditioner(),
			_settings,
			_catalog,
			_cache,
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
			new(factory, _backendSelector, new VocabularyConditioner(), _settings, _catalog, _cache, Options.Create(options));

		await transcriber.TranscribeAsync(Clip(), CancellationToken.None);
		factory.LastDecodingOptions!.InitialPrompt.Should().Contain("Reqnroll");

		options.CustomVocabulary = ["Velopack"];
		await transcriber.TranscribeAsync(Clip(), CancellationToken.None);

		factory.LastDecodingOptions!.InitialPrompt.Should().Contain("Velopack");
		factory.CreateCount.Should().Be(1);
	}

	[Fact]
	public async Task Resolves_the_active_model_from_settings_when_no_explicit_path_is_configured()
	{
		// WHISPER-87: with no config override, the transcriber loads the model the user actually selected,
		// resolved through settings -> catalog -> cache (the same path the doctor's model check resolves).
		string modelFile = ExistingModelFile();
		WhisperModelCatalogEntry entry = new("base.en", "Base (English)", "q5", "ggml-base.en.bin", 1);
		_settings.LoadAsync(Arg.Any<CancellationToken>())
			.Returns(new AppSettings("base.en", HotkeyBinding.Parse("Ctrl+Win"), 500, false));
		_catalog.Find("base.en").Returns(entry);
		_cache.GetCachedPath(entry).Returns(modelFile);
		FakeWhisperEngineFactory factory = new(new WhisperSegment("hi", TimeSpan.Zero, TimeSpan.Zero, 1f));
		await using WhisperTranscriber transcriber = CreateTranscriber(factory, modelPath: string.Empty);

		TranscriptionResult result = await transcriber.TranscribeAsync(Clip(), CancellationToken.None);

		factory.LastModelPath.Should().Be(modelFile);
		result.Text.Should().Be("hi");
	}

	[Fact]
	public async Task Reloads_the_engine_when_the_active_model_changes()
	{
		// WHISPER-87: switching the active model makes the next transcription load the new model.
		string firstFile = ExistingModelFile();
		string secondFile = ExistingModelFile();
		WhisperModelCatalogEntry first = new("base.en", "Base", "q5", "ggml-base.en.bin", 1);
		WhisperModelCatalogEntry second = new("small.en", "Small", "q5", "ggml-small.en.bin", 1);
		_catalog.Find("base.en").Returns(first);
		_catalog.Find("small.en").Returns(second);
		_cache.GetCachedPath(first).Returns(firstFile);
		_cache.GetCachedPath(second).Returns(secondFile);
		FakeWhisperEngineFactory factory = new(new WhisperSegment("x", TimeSpan.Zero, TimeSpan.Zero, 1f));
		await using WhisperTranscriber transcriber = CreateTranscriber(factory, modelPath: string.Empty);

		_settings.LoadAsync(Arg.Any<CancellationToken>())
			.Returns(new AppSettings("base.en", HotkeyBinding.Parse("Ctrl+Win"), 500, false));
		await transcriber.TranscribeAsync(Clip(), CancellationToken.None);

		_settings.LoadAsync(Arg.Any<CancellationToken>())
			.Returns(new AppSettings("small.en", HotkeyBinding.Parse("Ctrl+Win"), 500, false));
		await transcriber.TranscribeAsync(Clip(), CancellationToken.None);

		factory.LastModelPath.Should().Be(secondFile);
		factory.CreateCount.Should().Be(2);
	}

	[Fact]
	public async Task Preload_loads_the_model_and_runs_a_warm_up_inference()
	{
		// WHISPER-127: warm-up loads the model and runs one throwaway inference so the first real dictation
		// pays no cold-load cost. It is independent of the live custom vocabulary (warm-up uses defaults).
		FakeWhisperEngineFactory factory = new(new WhisperSegment("x", TimeSpan.Zero, TimeSpan.Zero, 1f));
		await using WhisperTranscriber transcriber = CreateTranscriber(factory, ExistingModelFile(), vocabulary: ["Reqnroll"]);

		await transcriber.PreloadAsync(CancellationToken.None);

		factory.CreateCount.Should().Be(1);
		factory.LastDecodingOptions.Should().Be(DecodingOptions.Default);
	}

	[Fact]
	public async Task Preload_then_transcribe_reuses_the_warmed_engine_without_a_cold_load()
	{
		// WHISPER-127: the whole point — the first real dictation after warm-up reuses the loaded engine.
		FakeWhisperEngineFactory factory = new(new WhisperSegment("hi", TimeSpan.Zero, TimeSpan.Zero, 1f));
		await using WhisperTranscriber transcriber = CreateTranscriber(factory, ExistingModelFile());

		await transcriber.PreloadAsync(CancellationToken.None);
		TranscriptionResult result = await transcriber.TranscribeAsync(Clip(), CancellationToken.None);

		factory.CreateCount.Should().Be(1);
		result.Text.Should().Be("hi");
	}

	[Fact]
	public async Task Preload_raises_a_typed_error_when_no_model_is_available()
	{
		// A fresh install has no model yet; warm-up surfaces the same typed error a transcription would, for
		// the startup warm-up service to swallow.
		FakeWhisperEngineFactory factory = new();
		await using WhisperTranscriber transcriber = CreateTranscriber(factory, modelPath: string.Empty);

		Func<Task> act = async () => await transcriber.PreloadAsync(CancellationToken.None);

		await act.Should().ThrowAsync<ModelNotFoundException>();
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
