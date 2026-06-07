// Internal seam over the native Silero VAD inference session. It scores one fixed-size window at a
// time and carries the model's recurrent state between windows (hence Reset between clips). Splitting
// it out lets SileroVad's windowing logic be tested without the ONNX model present: the specs feed a
// fake session, while OnnxVadSession runs the real model.

namespace Infrastructure.Audio;

public interface IVadSession : IDisposable
{
	/// <summary>Number of samples the model scores per window (e.g. 512 at 16 kHz).</summary>
	int WindowSamples { get; }

	/// <summary>Returns the speech probability [0,1] for one window, advancing recurrent state.</summary>
	float Next(ReadOnlyMemory<float> window);

	/// <summary>Clears the recurrent state so the next clip is scored independently.</summary>
	void Reset();
}
