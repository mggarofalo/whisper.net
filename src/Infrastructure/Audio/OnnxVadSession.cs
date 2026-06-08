// The real Silero VAD session: runs the model fully on-device via ONNX Runtime (no network egress).
// It holds the model's recurrent state (h/c) across windows and reloads it on Reset. The model is
// loaded lazily on first use so composing the object graph never requires the asset to be present —
// only actual inference does.
//
// The Silero VAD ONNX asset (MIT, ~1.8 MB) ships with the app as a content asset — never downloaded —
// resolved at AppContext.BaseDirectory/assets/silero_vad.onnx (WHISPER-66). Real inference over the
// bundled model is covered by the @slow VadRealModelTests; the headless specs drive SileroVad over a
// fake session instead.

using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Infrastructure.Audio;

internal sealed class OnnxVadSession(string modelPath) : IVadSession
{
	private const int SampleRate = 16_000;
	private const int StateSize = 2 * 1 * 64;

	private InferenceSession? _session;
	private float[] _h = new float[StateSize];
	private float[] _c = new float[StateSize];

	public int WindowSamples => 512;

	public void Reset()
	{
		Array.Clear(_h);
		Array.Clear(_c);
	}

	public float Next(ReadOnlyMemory<float> window)
	{
		_session ??= new InferenceSession(modelPath);

		List<NamedOnnxValue> inputs =
		[
			NamedOnnxValue.CreateFromTensor("input", new DenseTensor<float>(window.ToArray(), [1, window.Length])),
			NamedOnnxValue.CreateFromTensor("sr", new DenseTensor<long>(new long[] { SampleRate }, [1])),
			NamedOnnxValue.CreateFromTensor("h", new DenseTensor<float>(_h, [2, 1, 64])),
			NamedOnnxValue.CreateFromTensor("c", new DenseTensor<float>(_c, [2, 1, 64])),
		];

		using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _session.Run(inputs);

		float probability = results.First(r => r.Name == "output").AsTensor<float>()[0];
		_h = results.First(r => r.Name == "hn").AsTensor<float>().ToArray();
		_c = results.First(r => r.Name == "cn").AsTensor<float>().ToArray();

		return probability;
	}

	public void Dispose() => _session?.Dispose();
}
