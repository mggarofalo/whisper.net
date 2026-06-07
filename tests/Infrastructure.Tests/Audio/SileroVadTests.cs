// Edge-case depth for the WHISPER-31 VAD adapter's windowing, over a fake inference session (no ONNX
// model). Confirms the clip is sliced into full windows, each scored once in order, state is reset
// per analysis, a trailing partial window is dropped, and cancellation is honored.

using AwesomeAssertions;
using Domain.Audio;
using Infrastructure.Audio;
using Xunit;

namespace Infrastructure.Tests.Audio;

public sealed class SileroVadTests
{
	// A fake session that returns a queued score per window and records how it was driven.
	private sealed class FakeSession(int windowSamples, params float[] scores) : IVadSession
	{
		private int _index;

		public int WindowSamples { get; } = windowSamples;
		public int ResetCount { get; private set; }
		public List<int> WindowLengths { get; } = [];

		public void Reset() => ResetCount++;

		public float Next(ReadOnlyMemory<float> window)
		{
			WindowLengths.Add(window.Length);
			return _index < scores.Length ? scores[_index++] : 0f;
		}

		public void Dispose()
		{
		}
	}

	private static AudioClip Clip(int sampleCount) => new(new float[sampleCount], 16_000);

	[Fact]
	public async Task Scores_each_full_window_in_order()
	{
		FakeSession session = new(4, 0.1f, 0.9f);
		SileroVad vad = new(session);

		VadAnalysis analysis = await vad.AnalyzeAsync(Clip(8), CancellationToken.None);

		analysis.WindowProbabilities.Should().Equal(0.1f, 0.9f);
		analysis.WindowSamples.Should().Be(4);
		session.WindowLengths.Should().Equal(4, 4);
	}

	[Fact]
	public async Task Drops_a_trailing_partial_window()
	{
		FakeSession session = new(4, 0.5f, 0.5f);
		SileroVad vad = new(session);

		// 10 samples -> two full 4-sample windows; the last 2 samples are dropped.
		VadAnalysis analysis = await vad.AnalyzeAsync(Clip(10), CancellationToken.None);

		analysis.WindowProbabilities.Should().HaveCount(2);
	}

	[Fact]
	public async Task Resets_state_once_per_analysis()
	{
		FakeSession session = new(4, 0.5f);
		SileroVad vad = new(session);

		await vad.AnalyzeAsync(Clip(4), CancellationToken.None);
		await vad.AnalyzeAsync(Clip(4), CancellationToken.None);

		session.ResetCount.Should().Be(2);
	}

	[Fact]
	public async Task Honors_cancellation()
	{
		FakeSession session = new(4, 0.5f, 0.5f, 0.5f);
		SileroVad vad = new(session);
		using CancellationTokenSource cts = new();
		await cts.CancelAsync();

		Func<Task> act = async () => await vad.AnalyzeAsync(Clip(12), cts.Token);

		await act.Should().ThrowAsync<OperationCanceledException>();
	}
}
