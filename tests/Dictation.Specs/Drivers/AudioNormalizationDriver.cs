// Drives the normalization scenarios against the REAL Logic.AudioManagement behaviors:
// the AudioResampler (resolved from DI) and a CaptureBuffer constructed with the scenario's buffering
// options. No device is involved — frames are synthetic. Idle/recorded audio is filled with an
// increasing counter so the preroll assertion can prove the *most recent* samples were retained.

using AwesomeAssertions;
using Domain.Audio;
using Logic.AudioManagement;

namespace Dictation.Specs.Drivers;

public sealed class AudioNormalizationDriver(AudioResampler resampler)
{
	private const int TargetRate = 16_000;

	private int _pendingRate;
	private int _pendingChannels;
	private float[] _normalized = [];

	private AudioBufferingOptions _options = new();
	private CaptureBuffer? _buffer;
	private float _sampleCounter;
	private bool _limitReported;
	private AudioClip? _clip;

	private CaptureBuffer Buffer => _buffer ?? throw new InvalidOperationException("Configure the buffer first.");

	private static int MsToSamples(int ms) => ms * TargetRate / 1000;

	// --- normalization (resampler) ---

	public void PrepareOneSecondSource(int sampleRate, int channels)
	{
		_pendingRate = sampleRate;
		_pendingChannels = channels;
	}

	public void Normalize()
	{
		// One second of source audio = sampleRate frames * channels interleaved samples.
		float[] interleaved = new float[_pendingRate * _pendingChannels];
		CaptureFormat format = new(_pendingRate, _pendingChannels, 32, AudioSampleFormat.IeeeFloat);
		_normalized = resampler.ToMono(interleaved, format, TargetRate);
	}

	public void AssertOneSecondMono16k() => _normalized.Length.Should().Be(TargetRate);

	// --- buffering (CaptureBuffer) ---

	public void ConfigurePreroll(int ms)
	{
		_options = _options with { PrerollMs = ms };
		BuildBuffer();
	}

	public void ConfigureMaxDuration(int ms)
	{
		_options = _options with { MaxDurationMs = ms };
		BuildBuffer();
	}

	private void BuildBuffer()
	{
		_buffer = new CaptureBuffer(_options, resampler);
		_buffer.MaxDurationReached += (_, _) => _limitReported = true;
	}

	public void CaptureIdle(int ms) => Append(ms);

	public void StartRecording() => Buffer.StartRecording();

	public void RecordThenStop()
	{
		Buffer.StartRecording();
		_clip = Buffer.StopRecording();
	}

	public void CaptureWhileRecording(int ms) => Append(ms);

	// Append `ms` of 16 kHz mono audio (so it passes through the resampler unchanged), filled with an
	// increasing counter so retained samples are identifiable.
	private void Append(int ms)
	{
		int count = MsToSamples(ms);
		float[] samples = new float[count];
		for (int i = 0; i < count; i++)
		{
			samples[i] = _sampleCounter++;
		}

		Buffer.Append(samples, new CaptureFormat(TargetRate, 1, 32, AudioSampleFormat.IeeeFloat));
	}

	private AudioClip FinalClip() => _clip ??= Buffer.StopRecording();

	public void AssertRecordingIsRecentPreroll(int prerollMs)
	{
		int expected = MsToSamples(prerollMs);
		AudioClip clip = FinalClip();

		clip.Samples.Should().HaveCount(expected);
		// The retained preroll must be the MOST RECENT `expected` samples captured so far.
		clip.Samples[0].Should().Be(_sampleCounter - expected);
		clip.Samples[^1].Should().Be(_sampleCounter - 1);
	}

	public void AssertRecordingDurationMs(int ms) =>
		FinalClip().Samples.Should().HaveCount(MsToSamples(ms));

	public void AssertLimitReported() =>
		_limitReported.Should().BeTrue("the soft limit must be observable even though recording continues");
}
