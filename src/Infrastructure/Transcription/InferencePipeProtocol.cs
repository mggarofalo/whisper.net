// Versioned, length-prefixed local IPC framing. Metadata/results are compact JSON; PCM samples are a
// raw little-endian float payload so a long dictation is not expanded into JSON numbers or Base64.
// Strict size limits reject malformed/untrusted frames before allocating large buffers.

using System.Buffers.Binary;
using System.Text.Json;

namespace Infrastructure.Transcription;

public static class InferencePipeProtocol
{
	public const int Version = 1;
	public const int MaximumHeaderBytes = 64 * 1024;
	public const int MaximumResponseBytes = 4 * 1024 * 1024;
	public const int MaximumSamples = 16_000 * 60 * 10; // ten minutes at Whisper's 16 kHz input rate

	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public static async ValueTask WriteRequestAsync(
		Stream stream,
		InferenceWorkerRequest request,
		CancellationToken cancellationToken)
	{
		ValidateRequest(request);
		RequestHeader header = new(
			Version,
			request.Operation,
			request.ModelPath,
			request.Language,
			request.DecodingOptions,
			request.SampleRate,
			request.Samples.Length);
		byte[] headerBytes = JsonSerializer.SerializeToUtf8Bytes(header, JsonOptions);
		await WriteFrameAsync(stream, headerBytes, MaximumHeaderBytes, cancellationToken).ConfigureAwait(false);

		byte[] samples = new byte[checked(request.Samples.Length * sizeof(float))];
		Buffer.BlockCopy(request.Samples, 0, samples, 0, samples.Length);
		await stream.WriteAsync(samples, cancellationToken).ConfigureAwait(false);
		await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
	}

	public static async ValueTask<InferenceWorkerRequest> ReadRequestAsync(
		Stream stream,
		CancellationToken cancellationToken)
	{
		byte[] headerBytes = await ReadFrameAsync(stream, MaximumHeaderBytes, cancellationToken).ConfigureAwait(false);
		RequestHeader header = JsonSerializer.Deserialize<RequestHeader>(headerBytes, JsonOptions)
			?? throw new InvalidDataException("The inference request header was empty.");

		if (header.Version != Version)
		{
			throw new InvalidDataException($"Unsupported inference protocol version {header.Version}.");
		}

		if (header.SampleCount is < 0 or > MaximumSamples)
		{
			throw new InvalidDataException($"Invalid inference sample count {header.SampleCount}.");
		}

		byte[] sampleBytes = new byte[checked(header.SampleCount * sizeof(float))];
		await ReadExactlyAsync(stream, sampleBytes, cancellationToken).ConfigureAwait(false);
		float[] samples = new float[header.SampleCount];
		Buffer.BlockCopy(sampleBytes, 0, samples, 0, sampleBytes.Length);

		InferenceWorkerRequest request = new(
			header.ModelPath,
			header.Language,
			header.DecodingOptions,
			header.SampleRate,
			samples,
			header.Operation);
		ValidateRequest(request);
		return request;
	}

	public static ValueTask WriteResponseAsync(
		Stream stream,
		InferenceWorkerResponse response,
		CancellationToken cancellationToken) =>
		WriteFrameAsync(
			stream,
			JsonSerializer.SerializeToUtf8Bytes(new ResponseEnvelope(Version, response), JsonOptions),
			MaximumResponseBytes,
			cancellationToken);

	public static async ValueTask<InferenceWorkerResponse> ReadResponseAsync(
		Stream stream,
		CancellationToken cancellationToken)
	{
		byte[] bytes = await ReadFrameAsync(stream, MaximumResponseBytes, cancellationToken).ConfigureAwait(false);
		ResponseEnvelope envelope = JsonSerializer.Deserialize<ResponseEnvelope>(bytes, JsonOptions)
			?? throw new InvalidDataException("The inference response was empty.");
		if (envelope.Version != Version)
		{
			throw new InvalidDataException($"Unsupported inference protocol version {envelope.Version}.");
		}

		return envelope.Response;
	}

	private static void ValidateRequest(InferenceWorkerRequest request)
	{
		if (request.Samples.Length > MaximumSamples)
		{
			throw new InvalidDataException($"Inference requests may contain at most {MaximumSamples} samples.");
		}

		if (request.Operation is not InferenceOperation.Shutdown && string.IsNullOrWhiteSpace(request.ModelPath))
		{
			throw new InvalidDataException("A model path is required for inference operations.");
		}

		if (request.SampleRate <= 0 && request.Operation is InferenceOperation.Transcribe or InferenceOperation.WarmUp)
		{
			throw new InvalidDataException("A positive sample rate is required for inference.");
		}
	}

	private static async ValueTask WriteFrameAsync(
		Stream stream,
		byte[] payload,
		int maximumBytes,
		CancellationToken cancellationToken)
	{
		if (payload.Length > maximumBytes)
		{
			throw new InvalidDataException($"IPC frame exceeds the {maximumBytes}-byte limit.");
		}

		byte[] length = new byte[sizeof(int)];
		BinaryPrimitives.WriteInt32LittleEndian(length, payload.Length);
		await stream.WriteAsync(length, cancellationToken).ConfigureAwait(false);
		await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
		await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
	}

	private static async ValueTask<byte[]> ReadFrameAsync(
		Stream stream,
		int maximumBytes,
		CancellationToken cancellationToken)
	{
		byte[] lengthBytes = new byte[sizeof(int)];
		await ReadExactlyAsync(stream, lengthBytes, cancellationToken).ConfigureAwait(false);
		int length = BinaryPrimitives.ReadInt32LittleEndian(lengthBytes);
		if (length < 0 || length > maximumBytes)
		{
			throw new InvalidDataException($"Invalid IPC frame length {length}.");
		}

		byte[] payload = new byte[length];
		await ReadExactlyAsync(stream, payload, cancellationToken).ConfigureAwait(false);
		return payload;
	}

	private static async ValueTask ReadExactlyAsync(
		Stream stream,
		Memory<byte> buffer,
		CancellationToken cancellationToken)
	{
		int offset = 0;
		while (offset < buffer.Length)
		{
			int read = await stream.ReadAsync(buffer[offset..], cancellationToken).ConfigureAwait(false);
			if (read == 0)
			{
				throw new EndOfStreamException("The inference worker disconnected mid-message.");
			}

			offset += read;
		}
	}

	private sealed record RequestHeader(
		int Version,
		InferenceOperation Operation,
		string ModelPath,
		string? Language,
		Domain.Models.DecodingOptions DecodingOptions,
		int SampleRate,
		int SampleCount);

	private sealed record ResponseEnvelope(int Version, InferenceWorkerResponse Response);
}
