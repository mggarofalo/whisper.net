// Validates real on-device inference: runs the bundled Silero VAD ONNX
// model for real, end to end, through the exact production composition. IVad is resolved from a host
// built by the same AddWhisperServices extension the WPF app uses, so this also proves the bundled
// asset resolves at AppContext.BaseDirectory/assets/silero_vad.onnx (the path OnnxVadSession composes).
//
// Tagged @slow (Trait Category=slow): it loads the ~1.8 MB model and runs ONNX inference, so it is
// excluded from the fast PR/release gate (Category!=wip&Category!=slow) and run in its own CI step.
// No network egress: the model is a committed content asset, never downloaded.

using Application.Ports;
using Domain.Audio;
using Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Hosting.Tests;

[Trait("Category", "slow")]
public sealed class VadRealModelTests
{
	private const float SpeechThreshold = 0.5f;

	private static IVad ResolveVad()
	{
		HostApplicationBuilder builder = Host.CreateApplicationBuilder();
		builder.Configuration.AddInMemoryCollection([]);
		builder.Services.AddWhisperServices(builder.Configuration);

		// The host is intentionally not disposed within the test body: IVad and its ONNX session live for
		// the duration of the analysis below, and the process exits at test end.
		return builder.Build().Services.GetRequiredService<IVad>();
	}

	// Real speech (a TTS-rendered pangram) must be detected: the model reports high probabilities over the
	// voiced windows. Thresholds are deliberately slack relative to the observed values (max ~1.0, ~73% of
	// windows over 0.5) so the assertion is robust, not brittle.
	[Fact]
	public async Task Detects_speech_in_a_real_recording()
	{
		IVad vad = ResolveVad();
		AudioClip clip = LoadWav(FixturePath("speech.wav"));

		VadAnalysis analysis = await vad.AnalyzeAsync(clip, CancellationToken.None);

		Assert.NotEmpty(analysis.WindowProbabilities);
		Assert.All(analysis.WindowProbabilities, p => Assert.InRange(p, 0f, 1f));
		Assert.True(
			analysis.WindowProbabilities.Max() > SpeechThreshold,
			$"expected at least one window above {SpeechThreshold}; max was {analysis.WindowProbabilities.Max():F3}");

		double speechFraction = analysis.WindowProbabilities.Count(p => p > SpeechThreshold)
			/ (double)analysis.WindowProbabilities.Count;
		Assert.True(speechFraction >= 0.25, $"expected a meaningful speech fraction; got {speechFraction:P0}");
	}

	// Pure silence must score below the speech threshold on every window — the complement of the check
	// above, and the basis of the silence-gating policy. Confirms the model discriminates
	// rather than returning a constant.
	[Fact]
	public async Task Scores_silence_below_the_speech_threshold()
	{
		IVad vad = ResolveVad();
		AudioClip silence = new(new float[16_000 * 2], 16_000);

		VadAnalysis analysis = await vad.AnalyzeAsync(silence, CancellationToken.None);

		Assert.NotEmpty(analysis.WindowProbabilities);
		Assert.All(analysis.WindowProbabilities, p => Assert.True(
			p < SpeechThreshold, $"silence window scored {p:F3}, at/above the {SpeechThreshold} threshold"));
	}

	private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "Audio", name);

	// A minimal reader for the fixture's known shape: 16-bit PCM mono WAV. Walks the RIFF chunks once,
	// reading the format fields from the `fmt ` chunk and the samples from the `data` chunk (rather than
	// from fixed absolute offsets, which only hold when `fmt ` is the first chunk). Converts the 16-bit
	// samples to normalized floats — enough for this test, not a general WAV decoder (NAudio stays out of
	// the test project for one fixture).
	private static AudioClip LoadWav(string path)
	{
		byte[] bytes = File.ReadAllBytes(path);
		int sampleRate = 0;
		short bitsPerSample = 0;

		int offset = 12;
		while (offset + 8 <= bytes.Length)
		{
			string chunkId = System.Text.Encoding.ASCII.GetString(bytes, offset, 4);
			int chunkSize = BitConverter.ToInt32(bytes, offset + 4);
			int dataStart = offset + 8;

			if (chunkId == "fmt ")
			{
				sampleRate = BitConverter.ToInt32(bytes, dataStart + 4);
				bitsPerSample = BitConverter.ToInt16(bytes, dataStart + 14);
			}
			else if (chunkId == "data")
			{
				if (bitsPerSample != 16)
				{
					throw new NotSupportedException($"fixture must be 16-bit PCM; was {bitsPerSample}-bit.");
				}

				int sampleCount = chunkSize / 2;
				float[] samples = new float[sampleCount];
				for (int i = 0; i < sampleCount; i++)
				{
					samples[i] = BitConverter.ToInt16(bytes, dataStart + (i * 2)) / 32768f;
				}

				return new AudioClip(samples, sampleRate);
			}

			offset = dataStart + chunkSize + (chunkSize & 1);
		}

		throw new InvalidDataException("no data chunk found in WAV fixture.");
	}
}
