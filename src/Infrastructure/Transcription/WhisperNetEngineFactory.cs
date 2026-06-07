// Loads a Whisper.net model from a local file onto the selected backend. It sets the native runtime
// order BEFORE the factory loads the library — Vulkan first (with CPU fallback) when the GPU contact
// point chose the GPU, CPU only otherwise — exactly as the WHISPER-65 spike proved. A load failure on
// an existing-but-unreadable file is converted into a typed ModelLoadException so callers never see a
// raw native crash. Loading reads the local file only; there is no network access.

using Domain.Models;
using Whisper.net;
using Whisper.net.LibraryLoader;

namespace Infrastructure.Transcription;

internal sealed class WhisperNetEngineFactory : IWhisperEngineFactory
{
	public IWhisperEngine Create(string modelPath, ComputeBackend backend, string? language)
	{
		RuntimeOptions.RuntimeLibraryOrder = backend == ComputeBackend.Vulkan
			? [RuntimeLibrary.Vulkan, RuntimeLibrary.Cpu]
			: [RuntimeLibrary.Cpu];

		try
		{
			WhisperFactory factory = WhisperFactory.FromPath(modelPath);
			return new WhisperNetEngine(factory, language);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new ModelLoadException(modelPath, ex);
		}
	}
}
