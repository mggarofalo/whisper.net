// Drives the voice-activity scenarios end to end over a fake inference session: it builds
// a clip from speech/silence regions (speech filled 1.0 so preserved speech is countable), scores it
// through the REAL SileroVad adapter, then gates/trims it with the REAL VadSilencePolicy. One window
// per second keeps the second-based scenarios easy to read.

using AwesomeAssertions;
using Dictation.Specs.Support;
using Domain.Audio;
using Infrastructure.Audio;
using Logic.AudioManagement;

namespace Dictation.Specs.Drivers;

public sealed class VadDriver(VadSilencePolicy policy)
{
	private const int Rate = 16_000;
	private const int WindowSamples = 16_000; // one window == one second

	private readonly List<float> _samples = [];
	private readonly List<float> _scores = [];
	private int _speechSamples;
	private VadOptions _options = new();
	private VadSegment? _segment;
	private VadAnalysis? _analysis;

	public void AddSpeechSeconds(int seconds) => AddRegion(seconds, isSpeech: true);

	public void AddSilenceSeconds(int seconds) => AddRegion(seconds, isSpeech: false);

	private void AddRegion(int seconds, bool isSpeech)
	{
		float marker = isSpeech ? 1f : 0f;
		for (int second = 0; second < seconds; second++)
		{
			_scores.Add(marker);
			for (int i = 0; i < WindowSamples; i++)
			{
				_samples.Add(marker);
			}
		}

		if (isSpeech)
		{
			_speechSamples += seconds * WindowSamples;
		}
	}

	public void SetMidCollapseSeconds(int seconds) => _options = _options with { MidSilenceCollapseMs = seconds * 1000 };

	public async Task Analyze()
	{
		AudioClip clip = new(_samples.ToArray(), Rate);
		SileroVad vad = new(new FakeVadSession(WindowSamples, _scores.ToArray()));
		_analysis = await vad.AnalyzeAsync(clip, CancellationToken.None);
		_segment = policy.Apply(clip, _analysis, _options);
	}

	private VadSegment Segment => _segment ?? throw new InvalidOperationException("Analyze first.");

	public void AssertGatedOut()
	{
		Segment.ContainsSpeech.Should().BeFalse();
		Segment.Trimmed.Samples.Should().BeEmpty();
	}

	public void AssertTrimmedSeconds(int seconds)
	{
		Segment.ContainsSpeech.Should().BeTrue();
		Segment.Trimmed.Samples.Should().HaveCount(seconds * WindowSamples);
	}

	public void AssertSpeechPreserved() =>
		Segment.Trimmed.Samples.Count(s => s == 1f).Should().Be(_speechSamples);

	public void AssertSpeechWindowsAre(params int[] expected)
	{
		VadAnalysis analysis = _analysis ?? throw new InvalidOperationException("Analyze first.");
		IEnumerable<int> speechWindows = analysis.WindowProbabilities
			.Select((probability, index) => (probability, index))
			.Where(w => w.probability >= _options.SpeechThreshold)
			.Select(w => w.index);

		speechWindows.Should().Equal(expected);
	}
}
