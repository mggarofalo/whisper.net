// IModelRuntime over the same supervised worker used by dictation. Handles carry only the requested
// model identity; model memory and native resources live exclusively in the child process.

using Application.Ports;
using Domain.Audio;
using Domain.Models;

namespace Infrastructure.Transcription;

public sealed class WorkerModelRuntime(IInferenceWorkerSupervisor supervisor) : IModelRuntime
{
	public async ValueTask<IModelHandle> LoadAsync(ModelLoadRequest request, CancellationToken cancellationToken)
	{
		InferenceWorkerRequest load = CreateRequest(request, [], 1, InferenceOperation.Load);
		await supervisor.TranscribeAsync(load, cancellationToken).ConfigureAwait(false);
		return new WorkerModelHandle(supervisor, request);
	}

	private static InferenceWorkerRequest CreateRequest(
		ModelLoadRequest request,
		float[] samples,
		int sampleRate,
		InferenceOperation operation) =>
		new(request.ModelPath, request.Language, DecodingOptions.Default, sampleRate, samples, operation);

	private sealed class WorkerModelHandle(IInferenceWorkerSupervisor supervisor, ModelLoadRequest request) : IModelHandle
	{
		private static readonly float[] WarmupSamples = new float[1_600];
		private int _disposed;

		public async ValueTask WarmUpAsync(CancellationToken cancellationToken)
		{
			InferenceWorkerRequest warmup = CreateRequest(request, WarmupSamples, 16_000, InferenceOperation.WarmUp);
			await supervisor.TranscribeAsync(warmup, cancellationToken).ConfigureAwait(false);
		}

		public ValueTask<TranscriptionResult> TranscribeAsync(AudioClip clip, CancellationToken cancellationToken) =>
			supervisor.TranscribeAsync(
				CreateRequest(request, clip.Samples as float[] ?? [.. clip.Samples], clip.SampleRate, InferenceOperation.Transcribe),
				cancellationToken);

		public async ValueTask DisposeAsync()
		{
			if (Interlocked.Exchange(ref _disposed, 1) != 0)
			{
				return;
			}

			if (!supervisor.HasLiveWorker)
			{
				return;
			}

			InferenceWorkerRequest unload = CreateRequest(request, [], 1, InferenceOperation.Unload);
			try
			{
				await supervisor.TranscribeAsync(unload, CancellationToken.None).ConfigureAwait(false);
			}
			catch (InferenceUnavailableException)
			{
				// The supervisor already discarded every failed generation; there is nothing left to unload.
			}
		}
	}
}
