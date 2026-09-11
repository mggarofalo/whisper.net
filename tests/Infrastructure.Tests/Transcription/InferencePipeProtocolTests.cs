using AwesomeAssertions;
using Domain.Audio;
using Domain.Models;
using Infrastructure.Transcription;
using Xunit;

namespace Infrastructure.Tests.Transcription;

public sealed class InferencePipeProtocolTests
{
	[Fact]
	public async Task Round_trips_metadata_and_raw_pcm_samples()
	{
		InferenceWorkerRequest expected = new(
			"C:/models/base.bin",
			"en",
			new DecodingOptions("Reqnroll", true),
			16_000,
			[0.1f, -0.2f, 0.3f]);
		await using MemoryStream stream = new();

		await InferencePipeProtocol.WriteRequestAsync(stream, expected, CancellationToken.None);
		stream.Position = 0;
		InferenceWorkerRequest actual = await InferencePipeProtocol.ReadRequestAsync(stream, CancellationToken.None);

		actual.Should().BeEquivalentTo(expected);
	}

	[Fact]
	public async Task Rejects_an_oversized_frame_before_allocating_its_payload()
	{
		await using MemoryStream stream = new();
		byte[] invalidLength = BitConverter.GetBytes(InferencePipeProtocol.MaximumHeaderBytes + 1);
		await stream.WriteAsync(invalidLength, CancellationToken.None);
		stream.Position = 0;

		Func<Task> act = async () =>
			await InferencePipeProtocol.ReadRequestAsync(stream, CancellationToken.None);

		await act.Should().ThrowAsync<InvalidDataException>();
	}

	[Fact]
	public async Task Round_trips_a_transcription_response_with_segments()
	{
		InferenceWorkerResponse expected = new(
			true,
			new TranscriptionResult(
				"hello",
				[new TranscriptionSegment("hello", TimeSpan.Zero, TimeSpan.FromSeconds(1), 0.9f)]));
		await using MemoryStream stream = new();

		await InferencePipeProtocol.WriteResponseAsync(stream, expected, CancellationToken.None);
		stream.Position = 0;
		InferenceWorkerResponse actual = await InferencePipeProtocol.ReadResponseAsync(stream, CancellationToken.None);

		actual.Should().BeEquivalentTo(expected);
	}
}
