// The capture normalization + buffering stage. It receives raw device frames, normalizes each to the
// target mono rate (via AudioResampler), and manages two buffers:
//
//   * a fixed-size circular PREROLL ring that, while idle, always holds the most recent N ms of audio
//     so speech that started just before the user triggered recording isn't lost; and
//   * a growable RECORDING buffer that, once recording starts, is seeded with the preroll and then
//     accumulates frames for as long as the user keeps dictating.
//
// The max duration is a SOFT limit (WHISPER-111): whisper.cpp handles arbitrary-length clips
// internally, so nothing is ever dropped — the buffer keeps growing past the limit, and the
// NearMaxDuration / MaxDurationReached events (each firing once per recording, at 80% and 100% of the
// limit) let the orchestration layer warn the user instead. The preroll ring stays fixed-capacity and
// the recording list is capacity-hinted and reused across recordings, so continuous capture stays
// allocation-conscious. Pure logic — no device dependency — so it's driven in tests by synthetic
// frames. Connecting it to a real IAudioSource is the orchestration layer's job (Module 7).

using Domain.Audio;

namespace Logic.AudioManagement;

public sealed class CaptureBuffer
{
	// Bound the recording list's up-front capacity hint to 30 seconds of audio: the soft limit can be
	// long (the 600 s default), and pre-allocating it in full would pin tens of megabytes per buffer.
	private const int CapacityHintSeconds = 30;

	private readonly AudioBufferingOptions _options;
	private readonly AudioResampler _resampler;

	private readonly float[] _preroll; // circular ring of the most recent idle audio
	private int _prerollCount;
	private int _prerollHead; // index of the oldest retained preroll sample

	private readonly List<float> _recording; // growable accumulation buffer, reused across recordings
	private readonly int _maxDurationSamples; // the soft limit (WHISPER-111), in target-rate samples
	private readonly int _nearMaxDurationSamples; // 80% of the soft limit, in target-rate samples
	private bool _isRecording;
	private bool _nearMaxDurationFired;
	private bool _maxDurationFired;

	public CaptureBuffer(AudioBufferingOptions options, AudioResampler resampler)
	{
		_options = options;
		_resampler = resampler;

		// Long arithmetic: the default soft limit (600 s at 16 kHz) overflows a 32-bit ms * rate product.
		int prerollSamples = (int)Math.Max(0, (long)options.PrerollMs * options.TargetSampleRate / 1000);
		_maxDurationSamples = (int)Math.Max(1, (long)options.MaxDurationMs * options.TargetSampleRate / 1000);
		_nearMaxDurationSamples = (int)((long)_maxDurationSamples * 8 / 10);
		_preroll = new float[prerollSamples];
		_recording = new List<float>((int)Math.Min(_maxDurationSamples, CapacityHintSeconds * (long)Math.Max(0, options.TargetSampleRate)));
	}

	/// <summary>True between <see cref="StartRecording"/> and <see cref="StopRecording"/>.</summary>
	public bool IsRecording => _isRecording;

	/// <summary>The duration of the audio recorded so far, in milliseconds at the target rate.</summary>
	public int RecordedDurationMs => _options.TargetSampleRate > 0
		? (int)((long)_recording.Count * 1000 / _options.TargetSampleRate)
		: 0;

	/// <summary>
	/// Raised once per recording when it reaches 80% of the soft max-duration limit (WHISPER-111), so
	/// the user can be warned before the limit. Recording continues; nothing is dropped.
	/// </summary>
	public event EventHandler? NearMaxDuration;

	/// <summary>
	/// Raised once per recording when it reaches the soft max-duration limit. The limit is soft
	/// (WHISPER-111): recording continues and every later sample is retained.
	/// </summary>
	public event EventHandler? MaxDurationReached;

	/// <summary>
	/// Feed one buffer of captured frames. While idle they refresh the preroll ring; while recording
	/// they accumulate into the segment.
	/// </summary>
	public void Append(ReadOnlySpan<float> interleaved, CaptureFormat format)
	{
		float[] normalized = _resampler.ToMono(interleaved, format, _options.TargetSampleRate);

		if (_isRecording)
		{
			AppendToRecording(normalized);
		}
		else
		{
			AppendToPreroll(normalized);
		}
	}

	/// <summary>Begin a recording, seeding it with the retained preroll so speech onset is preserved.</summary>
	public void StartRecording()
	{
		if (_isRecording)
		{
			return;
		}

		_recording.Clear();
		_nearMaxDurationFired = false;
		_maxDurationFired = false;

		// Copy the preroll ring (oldest -> newest) to the front of the recording buffer.
		for (int i = 0; i < _prerollCount; i++)
		{
			_recording.Add(_preroll[(_prerollHead + i) % _preroll.Length]);
		}

		_isRecording = true;
	}

	/// <summary>Finalize the current recording and return it as a normalized clip. Resets for reuse.</summary>
	public AudioClip StopRecording()
	{
		_isRecording = false;
		float[] samples = _recording.ToArray();
		_recording.Clear();
		return new AudioClip(samples, _options.TargetSampleRate);
	}

	private void AppendToRecording(float[] normalized)
	{
		_recording.AddRange(normalized);

		if (!_nearMaxDurationFired && _recording.Count >= _nearMaxDurationSamples)
		{
			_nearMaxDurationFired = true;
			NearMaxDuration?.Invoke(this, EventArgs.Empty);
		}

		if (!_maxDurationFired && _recording.Count >= _maxDurationSamples)
		{
			_maxDurationFired = true;
			MaxDurationReached?.Invoke(this, EventArgs.Empty);
		}
	}

	private void AppendToPreroll(float[] normalized)
	{
		if (_preroll.Length == 0)
		{
			return;
		}

		foreach (float sample in normalized)
		{
			if (_prerollCount < _preroll.Length)
			{
				_preroll[(_prerollHead + _prerollCount) % _preroll.Length] = sample;
				_prerollCount++;
			}
			else
			{
				// Ring full: overwrite the oldest sample and advance the head.
				_preroll[_prerollHead] = sample;
				_prerollHead = (_prerollHead + 1) % _preroll.Length;
			}
		}
	}
}
