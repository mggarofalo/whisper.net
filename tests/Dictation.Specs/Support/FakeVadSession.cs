// A stand-in for the native Silero inference session: returns a preset speech score per window so the
// The scenarios can drive the REAL SileroVad adapter (windowing) and the REAL VadSilencePolicy
// (gate/trim) without the ONNX model. Reset (called by SileroVad at the start of each analysis) rewinds
// the score sequence.

using Infrastructure.Audio;

namespace Dictation.Specs.Support;

public sealed class FakeVadSession(int windowSamples, float[] scores) : IVadSession
{
	private int _index;

	public int WindowSamples { get; } = windowSamples;

	public void Reset() => _index = 0;

	public float Next(ReadOnlyMemory<float> window) => _index < scores.Length ? scores[_index++] : 0f;

	public void Dispose()
	{
	}
}
