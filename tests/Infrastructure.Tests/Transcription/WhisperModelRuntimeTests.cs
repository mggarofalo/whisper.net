// Inner TDD loop for the WHISPER-15 native runtime over the fake Whisper.net engine seam. Confirms a
// loaded handle folds the engine's segments into a TranscriptionResult and that warmup runs an
// inference without throwing — the device-independent parts of the runtime, with no real model.

using Application.Ports;
using AwesomeAssertions;
using Domain.Audio;
using Domain.Models;
using Infrastructure.Transcription;
using Xunit;

namespace Infrastructure.Tests.Transcription;

public sealed class WhisperModelRuntimeTests
{
	private static ModelLoadRequest Request() =>
		new("base", "C:/cache/ggml-base.bin", ComputeBackend.Cpu, ComputePrecision.Float16, "en");

	private static AudioClip Clip() => new([0.1f, 0.2f], 16_000);

	[Fact]
	public async Task Loads_a_handle_that_folds_segments_into_text()
	{
		FakeWhisperEngineFactory factory = new(
			new WhisperSegment("hello ", TimeSpan.Zero, TimeSpan.FromSeconds(1), 0.9f),
			new WhisperSegment("world", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), 0.8f));
		WhisperModelRuntime runtime = new(factory);

		await using IModelHandle handle = await runtime.LoadAsync(Request(), CancellationToken.None);
		TranscriptionResult result = await handle.TranscribeAsync(Clip(), CancellationToken.None);

		result.Text.Should().Be("hello world");
		result.Segments.Should().HaveCount(2);
	}

	[Fact]
	public async Task Warmup_runs_an_inference_without_throwing()
	{
		FakeWhisperEngineFactory factory = new(new WhisperSegment("x", TimeSpan.Zero, TimeSpan.Zero, 1f));
		WhisperModelRuntime runtime = new(factory);

		await using IModelHandle handle = await runtime.LoadAsync(Request(), CancellationToken.None);
		Func<Task> act = async () => await handle.WarmUpAsync(CancellationToken.None);

		await act.Should().NotThrowAsync();
	}
}
