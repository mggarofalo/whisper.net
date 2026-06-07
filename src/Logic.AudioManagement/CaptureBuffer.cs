// The capture normalization + buffering stage. It receives raw device frames, normalizes each to the
// target mono rate (via AudioResampler), and manages two buffers:
//
//   * a fixed-size circular PREROLL ring that, while idle, always holds the most recent N ms of audio
//     so speech that started just before the user triggered recording isn't lost; and
//   * a bounded RECORDING buffer that, once recording starts, is seeded with the preroll and then
//     accumulates frames up to a max-duration safety cap.
//
// Both buffers are fixed-capacity and reused across recordings, so continuous capture never grows
// memory without bound. Pure logic — no device dependency — so it's driven in tests by synthetic
// frames. Connecting it to a real IAudioSource is the orchestration layer's job (Module 7).

using Domain.Audio;

namespace Logic.AudioManagement;

public sealed class CaptureBuffer
{
	private readonly AudioBufferingOptions _options;
	private readonly AudioResampler _resampler;

	private readonly float[] _preroll; // circular ring of the most recent idle audio
	private int _prerollCount;
	private int _prerollHead; // index of the oldest retained preroll sample

	private readonly float[] _recording; // bounded accumulation buffer, reused across recordings
	private int _recordedCount;
	private bool _isRecording;
	private bool _capped;

	public CaptureBuffer(AudioBufferingOptions options, AudioResampler resampler)
	{
		_options = options;
		_resampler = resampler;

		int prerollSamples = Math.Max(0, options.PrerollMs * options.TargetSampleRate / 1000);
		int maxSamples = Math.Max(1, options.MaxDurationMs * options.TargetSampleRate / 1000);
		_preroll = new float[prerollSamples];
		_recording = new float[maxSamples];
	}

	/// <summary>True between <see cref="StartRecording"/> and <see cref="StopRecording"/>.</summary>
	public bool IsRecording => _isRecording;

	/// <summary>Raised once when an in-progress recording hits the max-duration cap and is force-finalized.</summary>
	public event EventHandler? MaxDurationReached;

	/// <summary>
	/// Feed one buffer of captured frames. While idle they refresh the preroll ring; while recording
	/// they accumulate into the segment (until the cap is reached).
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

		_recordedCount = 0;
		_capped = false;

		// Copy the preroll ring (oldest -> newest) to the front of the recording buffer.
		for (int i = 0; i < _prerollCount && _recordedCount < _recording.Length; i++)
		{
			_recording[_recordedCount++] = _preroll[(_prerollHead + i) % _preroll.Length];
		}

		_isRecording = true;
	}

	/// <summary>Finalize the current recording and return it as a normalized clip. Resets for reuse.</summary>
	public AudioClip StopRecording()
	{
		_isRecording = false;
		float[] samples = _recording[.._recordedCount].ToArray();
		_recordedCount = 0;
		return new AudioClip(samples, _options.TargetSampleRate);
	}

	private void AppendToRecording(float[] normalized)
	{
		if (_capped)
		{
			return;
		}

		int space = _recording.Length - _recordedCount;
		int take = Math.Min(space, normalized.Length);
		Array.Copy(normalized, 0, _recording, _recordedCount, take);
		_recordedCount += take;

		if (_recordedCount >= _recording.Length)
		{
			_capped = true;
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
