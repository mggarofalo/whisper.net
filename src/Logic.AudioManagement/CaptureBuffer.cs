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
// limit) let the orchestration layer warn the user instead. A HARD failsafe ceiling backs the soft
// limit: HardLimitReached (once per recording) tells the orchestration to stop AND transcribe — the
// enforcement bound that keeps an unattended recording from growing without end, while still never
// discarding audio. The preroll ring stays fixed-capacity and the recording list is capacity-hinted,
// reused across recordings, and trimmed back to the hint when a long recording grew past it, so
// continuous capture stays allocation-conscious.
//
// THREADING: Append runs on the capture thread while StartRecording / StopRecording / DiscardRecording
// run on the orchestrator thread — and they genuinely overlap (frames keep flowing through the
// post-release grace window, WHISPER-112, and a cancel stops mid-stream), so every mutable field is
// guarded by one private lock. The limit events are raised OUTSIDE the lock (the firing decision is
// captured inside it) because their handlers re-enter the orchestration — the hard-limit handler stops
// this very buffer — and must never run with the recording store's lock held.
//
// Pure logic — no device dependency — so it's driven in tests by synthetic frames. Connecting it to a
// real IAudioSource is the orchestration layer's job (Module 7).

using Domain.Audio;

namespace Logic.AudioManagement;

public sealed class CaptureBuffer
{
	// Bound the recording list's up-front capacity hint to 30 seconds of audio: the soft limit can be
	// long (the 600 s default), and pre-allocating it in full would pin tens of megabytes per buffer.
	private const int CapacityHintSeconds = 30;

	private readonly AudioBufferingOptions _options;
	private readonly AudioResampler _resampler;

	// Guards every mutable field below (both buffers and the recording flags): see the THREADING note
	// in the header. Held only for the short mutations — never while raising an event.
	private readonly object _sync = new();

	private readonly float[] _preroll; // circular ring of the most recent idle audio
	private int _prerollCount;
	private int _prerollHead; // index of the oldest retained preroll sample

	private readonly List<float> _recording; // growable accumulation buffer, reused across recordings
	private readonly int _capacityHintSamples; // the reuse capacity the store is trimmed back to
	private readonly int _maxDurationSamples; // the soft limit (WHISPER-111), in target-rate samples
	private readonly int _nearMaxDurationSamples; // 80% of the soft limit, in target-rate samples
	private readonly int _hardMaxDurationSamples; // the hard failsafe ceiling, in target-rate samples
	private bool _isRecording;
	private bool _nearMaxDurationFired;
	private bool _maxDurationFired;
	private bool _hardLimitFired;

	public CaptureBuffer(AudioBufferingOptions options, AudioResampler resampler)
	{
		_options = options;
		_resampler = resampler;

		// Long arithmetic + clamp: the default soft limit (600 s at 16 kHz) overflows a 32-bit ms * rate
		// product, and an absurd configured duration would wrap the int cast — a negative threshold
		// fires every limit signal on the first sample (or constructs a negative-capacity list).
		// Clamping to Array.MaxLength turns any configuration into a usable ceiling instead.
		int prerollSamples = ToSampleCount(options.PrerollMs, options.TargetSampleRate, min: 0);
		_maxDurationSamples = ToSampleCount(options.MaxDurationMs, options.TargetSampleRate, min: 1);
		_nearMaxDurationSamples = (int)((long)_maxDurationSamples * 8 / 10);
		_hardMaxDurationSamples = ToSampleCount(options.HardMaxDurationMs, options.TargetSampleRate, min: 1);
		_capacityHintSamples = (int)Math.Min(_maxDurationSamples, CapacityHintSeconds * (long)Math.Max(0, options.TargetSampleRate));
		_preroll = new float[prerollSamples];
		_recording = new List<float>(_capacityHintSamples);
	}

	/// <summary>True between <see cref="StartRecording"/> and <see cref="StopRecording"/> (or <see cref="DiscardRecording"/>).</summary>
	public bool IsRecording
	{
		get
		{
			lock (_sync)
			{
				return _isRecording;
			}
		}
	}

	/// <summary>The duration of the audio recorded so far, in milliseconds at the target rate.</summary>
	public int RecordedDurationMs
	{
		get
		{
			if (_options.TargetSampleRate <= 0)
			{
				return 0;
			}

			lock (_sync)
			{
				return (int)((long)_recording.Count * 1000 / _options.TargetSampleRate);
			}
		}
	}

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
	/// Raised once per recording when it reaches the HARD failsafe ceiling
	/// (<see cref="AudioBufferingOptions.HardMaxDurationMs"/>). Unlike the soft-limit signals this one
	/// demands action: the orchestration must stop the recording through its normal stop path — stop
	/// and transcribe, never discard — so the buffer cannot grow without end. Raised on the capture
	/// thread, outside the buffer's lock. Re-armed by <see cref="StartRecording"/>.
	/// </summary>
	public event EventHandler? HardLimitReached;

	/// <summary>
	/// Feed one buffer of captured frames. While idle they refresh the preroll ring; while recording
	/// they accumulate into the segment.
	/// </summary>
	public void Append(ReadOnlySpan<float> interleaved, CaptureFormat format)
	{
		float[] normalized = _resampler.ToMono(interleaved, format, _options.TargetSampleRate);

		bool raiseNear = false;
		bool raiseMax = false;
		bool raiseHard = false;

		lock (_sync)
		{
			if (_isRecording)
			{
				_recording.AddRange(normalized);

				if (!_nearMaxDurationFired && _recording.Count >= _nearMaxDurationSamples)
				{
					_nearMaxDurationFired = true;
					raiseNear = true;
				}

				if (!_maxDurationFired && _recording.Count >= _maxDurationSamples)
				{
					_maxDurationFired = true;
					raiseMax = true;
				}

				if (!_hardLimitFired && _recording.Count >= _hardMaxDurationSamples)
				{
					_hardLimitFired = true;
					raiseHard = true;
				}
			}
			else
			{
				AppendToPreroll(normalized);
			}
		}

		// Raised outside the lock with the decisions captured above: the hard-limit handler stops this
		// very buffer, and even the tiny soft-limit handlers reach into the orchestration (messenger) —
		// none may run while the recording store's lock is held.
		if (raiseNear)
		{
			NearMaxDuration?.Invoke(this, EventArgs.Empty);
		}

		if (raiseMax)
		{
			MaxDurationReached?.Invoke(this, EventArgs.Empty);
		}

		if (raiseHard)
		{
			HardLimitReached?.Invoke(this, EventArgs.Empty);
		}
	}

	/// <summary>Begin a recording, seeding it with the retained preroll so speech onset is preserved.</summary>
	public void StartRecording()
	{
		lock (_sync)
		{
			if (_isRecording)
			{
				return;
			}

			_recording.Clear();
			_nearMaxDurationFired = false;
			_maxDurationFired = false;
			_hardLimitFired = false;

			// Copy the preroll ring (oldest -> newest) to the front of the recording buffer.
			for (int i = 0; i < _prerollCount; i++)
			{
				_recording.Add(_preroll[(_prerollHead + i) % _preroll.Length]);
			}

			_isRecording = true;
		}
	}

	/// <summary>Finalize the current recording and return it as a normalized clip. Resets for reuse.</summary>
	public AudioClip StopRecording()
	{
		float[] samples;
		lock (_sync)
		{
			_isRecording = false;
			samples = _recording.ToArray();
			ResetRecordingStore();
		}

		return new AudioClip(samples, _options.TargetSampleRate);
	}

	/// <summary>
	/// Abandon the current recording without materializing it: the discard counterpart of
	/// <see cref="StopRecording"/> for the paths whose clip would be thrown away (a cancel, a capture
	/// failure, a cancelled stop). Clears the store, releases grown capacity, and resets for reuse —
	/// no snapshot array is ever allocated for audio nobody will hear.
	/// </summary>
	public void DiscardRecording()
	{
		lock (_sync)
		{
			_isRecording = false;
			ResetRecordingStore();
		}
	}

	// Clear the recording store and release any capacity a long recording grew beyond the hint, so a
	// finalized or discarded marathon dictation doesn't pin its peak memory until the next one.
	// Callers hold _sync.
	private void ResetRecordingStore()
	{
		_recording.Clear();
		if (_recording.Capacity > _capacityHintSamples)
		{
			_recording.Capacity = _capacityHintSamples;
		}
	}

	// Duration -> target-rate sample count, in long arithmetic, clamped to a usable range so absurd
	// configuration can never wrap the int cast (see the constructor note).
	private static int ToSampleCount(int durationMs, int sampleRate, int min) =>
		(int)Math.Clamp((long)durationMs * sampleRate / 1000, min, Array.MaxLength);

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
