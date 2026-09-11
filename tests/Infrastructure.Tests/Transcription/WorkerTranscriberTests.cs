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

public sealed class WorkerTranscriberTests : IDisposable
{
	private readonly IInferenceWorkerSupervisor _supervisor = Substitute.For<IInferenceWorkerSupervisor>();
	private readonly ISettingsStore _settings = Substitute.For<ISettingsStore>();
	private readonly IModelCatalog _catalog = Substitute.For<IModelCatalog>();
	private readonly IModelCache _cache = Substitute.For<IModelCache>();
	private readonly List<string> _tempFiles = [];

	public WorkerTranscriberTests()
	{
		_supervisor.TranscribeAsync(Arg.Any<InferenceWorkerRequest>(), Arg.Any<CancellationToken>())
			.Returns(new TranscriptionResult("recognized"));
		_settings.LoadAsync(Arg.Any<CancellationToken>()).Returns(AppSettings.Default);
	}

	[Fact]
	public async Task Sends_local_audio_model_language_and_vocabulary_to_the_worker()
	{
		string modelPath = ExistingModelFile();
		WhisperOptions options = new()
		{
			ModelPath = modelPath,
			Language = "es",
			CustomVocabulary = ["Reqnroll"],
		};
		WorkerTranscriber transcriber = Create(options);

		TranscriptionResult result = await transcriber.TranscribeAsync(
			new AudioClip([0.1f, 0.2f], 16_000),
			CancellationToken.None);

		result.Text.Should().Be("recognized");
		await _supervisor.Received(1).TranscribeAsync(
			Arg.Is<InferenceWorkerRequest>(request =>
				request.Operation == InferenceOperation.Transcribe &&
				request.ModelPath == modelPath &&
				request.Language == "es" &&
				request.SampleRate == 16_000 &&
				request.Samples.SequenceEqual(new[] { 0.1f, 0.2f }) &&
				request.DecodingOptions.InitialPrompt!.Contains("Reqnroll", StringComparison.Ordinal)),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Resolves_the_active_model_when_no_override_is_configured()
	{
		string modelPath = ExistingModelFile();
		WhisperModelCatalogEntry entry = new("base.en", "Base", "q5", "ggml-base.en.bin", 1);
		_settings.LoadAsync(Arg.Any<CancellationToken>())
			.Returns(new AppSettings("base.en", HotkeyBinding.Parse("Ctrl+Win"), 500, false));
		_catalog.Find("base.en").Returns(entry);
		_cache.GetCachedPath(entry).Returns(modelPath);
		WorkerTranscriber transcriber = Create(new WhisperOptions());

		await transcriber.TranscribeAsync(new AudioClip([0.1f], 16_000), CancellationToken.None);

		await _supervisor.Received(1).TranscribeAsync(
			Arg.Is<InferenceWorkerRequest>(request => request.ModelPath == modelPath),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Missing_model_fails_before_starting_a_worker()
	{
		string missing = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.bin");
		WorkerTranscriber transcriber = Create(new WhisperOptions { ModelPath = missing });

		Func<Task> act = async () =>
			await transcriber.TranscribeAsync(new AudioClip([0.1f], 16_000), CancellationToken.None);

		await act.Should().ThrowAsync<ModelNotFoundException>();
		await _supervisor.DidNotReceive().TranscribeAsync(
			Arg.Any<InferenceWorkerRequest>(),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Preload_sends_a_warmup_request_to_the_worker()
	{
		WorkerTranscriber transcriber = Create(new WhisperOptions { ModelPath = ExistingModelFile() });

		await transcriber.PreloadAsync(CancellationToken.None);

		await _supervisor.Received(1).TranscribeAsync(
			Arg.Is<InferenceWorkerRequest>(request =>
				request.Operation == InferenceOperation.WarmUp &&
				request.SampleRate == 16_000 &&
				request.Samples.Length == 1_600),
			Arg.Any<CancellationToken>());
	}

	private WorkerTranscriber Create(WhisperOptions options) =>
		new(_supervisor, new VocabularyConditioner(), _settings, _catalog, _cache, Options.Create(options));

	private string ExistingModelFile()
	{
		string path = Path.GetTempFileName();
		_tempFiles.Add(path);
		return path;
	}

	public void Dispose()
	{
		foreach (string file in _tempFiles)
		{
			File.Delete(file);
		}
	}
}
