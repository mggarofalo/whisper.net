// Inner TDD loop for VadSilencePolicy: gating all-silence, trimming trailing silence, trimming
// leading silence down to the preroll, collapsing long internal pauses, and the speech threshold
// boundary. Uses a 1 kHz rate (1 sample == 1 ms) and 100-sample windows so window/ms arithmetic is
// obvious. Speech windows are filled with 1.0 and silence with 0.0 so preserved speech is countable.

using AwesomeAssertions;
using Domain.Audio;
using Xunit;

namespace Logic.AudioManagement.Tests;

public sealed class VadSilencePolicyTests
{
	private const int Rate = 1_000;
	private const int Window = 100;

	private readonly VadSilencePolicy _policy = new();

	// Build a clip + analysis from a window pattern: true = speech (filled 1.0), false = silence (0.0).
	private static (AudioClip Clip, VadAnalysis Analysis) Build(params bool[] windows)
	{
		List<float> samples = [];
		List<float> probabilities = [];
		foreach (bool speech in windows)
		{
			probabilities.Add(speech ? 1f : 0f);
			for (int i = 0; i < Window; i++)
			{
				samples.Add(speech ? 1f : 0f);
			}
		}

		return (new AudioClip(samples.ToArray(), Rate), new VadAnalysis(probabilities, Window));
	}

	[Fact]
	public void Gates_out_an_all_silence_segment()
	{
		(AudioClip clip, VadAnalysis analysis) = Build(false, false, false);

		VadSegment segment = _policy.Apply(clip, analysis, new VadOptions());

		segment.ContainsSpeech.Should().BeFalse();
		segment.Trimmed.Samples.Should().BeEmpty();
	}

	[Fact]
	public void Trims_trailing_silence_after_the_last_speech()
	{
		(AudioClip clip, VadAnalysis analysis) = Build(true, false, false); // speech + 200 ms silence

		VadSegment segment = _policy.Apply(clip, analysis, new VadOptions { LeadingKeepMs = 0 });

		segment.ContainsSpeech.Should().BeTrue();
		segment.Trimmed.Samples.Should().HaveCount(Window);
		segment.Trimmed.Samples.Should().OnlyContain(s => s == 1f);
	}

	[Fact]
	public void Trims_leading_silence_down_to_the_configured_preroll()
	{
		(AudioClip clip, VadAnalysis analysis) = Build(false, false, true); // 200 ms silence + speech

		VadSegment segment = _policy.Apply(clip, analysis, new VadOptions { LeadingKeepMs = 50 });

		// 50 ms of leading silence (preroll) + the 100 ms speech window.
		segment.Trimmed.Samples.Should().HaveCount(50 + Window);
		segment.Trimmed.Samples.Count(s => s == 1f).Should().Be(Window);
	}

	[Fact]
	public void Collapses_a_long_internal_pause_without_dropping_speech()
	{
		// speech, 300 ms pause, speech; collapse threshold 100 ms.
		(AudioClip clip, VadAnalysis analysis) = Build(true, false, false, false, true);

		VadSegment segment = _policy.Apply(clip, analysis, new VadOptions { LeadingKeepMs = 0, MidSilenceCollapseMs = 100 });

		// 100 (speech) + 100 (collapsed pause) + 100 (speech).
		segment.Trimmed.Samples.Should().HaveCount(300);
		segment.Trimmed.Samples.Count(s => s == 1f).Should().Be(2 * Window); // both speech portions intact
	}

	[Fact]
	public void Keeps_an_internal_pause_shorter_than_the_threshold_intact()
	{
		// speech, 100 ms pause, speech; collapse threshold 200 ms -> pause kept whole.
		(AudioClip clip, VadAnalysis analysis) = Build(true, false, true);

		VadSegment segment = _policy.Apply(clip, analysis, new VadOptions { LeadingKeepMs = 0, MidSilenceCollapseMs = 200 });

		segment.Trimmed.Samples.Should().HaveCount(3 * Window);
	}

	[Fact]
	public void Treats_a_window_at_the_threshold_as_speech()
	{
		(AudioClip clip, VadAnalysis analysis) = Build(false);
		VadAnalysis atThreshold = analysis with { WindowProbabilities = [0.5f] };

		VadSegment segment = _policy.Apply(clip, atThreshold, new VadOptions { SpeechThreshold = 0.5f, LeadingKeepMs = 0 });

		segment.ContainsSpeech.Should().BeTrue();
		segment.Trimmed.Samples.Should().HaveCount(Window);
	}
}
