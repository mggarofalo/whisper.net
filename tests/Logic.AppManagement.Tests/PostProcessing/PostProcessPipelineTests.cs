// Inner TDD loop for the post-process pipeline (WHISPER-41): it reads the live holder, normalizes with
// the configured filler toggle, applies the configured default transform after normalizing, and degrades
// safely (original normalized text) when the transform is unknown.

using Application.Configuration;
using Application.Ports;
using Application.Rephrase;
using AwesomeAssertions;
using Logic.AppManagement.OutputTransforms;
using Logic.AppManagement.PostProcessing;
using NSubstitute;
using Xunit;

namespace Logic.AppManagement.Tests.PostProcessing;

public sealed class PostProcessPipelineTests
{
	private readonly IFillerWordCleaner _cleaner = Substitute.For<IFillerWordCleaner>();
	private readonly IRephraseClient _rephraseClient = Substitute.For<IRephraseClient>();
	private readonly PostProcessSettingsHolder _holder = new();

	public PostProcessPipelineTests() =>
		_cleaner.Clean(Arg.Any<string>(), Arg.Any<bool>()).Returns(ci => $"normalized:{ci.ArgAt<string>(0)}");

	private PostProcessPipeline Pipeline() =>
		new(_cleaner, new OutputTransformService(new OutputTransformRegistry(), _rephraseClient), _holder);

	[Fact]
	public async Task Normalizes_with_the_configured_filler_toggle_and_skips_the_transform_when_none_is_set()
	{
		_holder.Current = new PostProcessOptions { RemoveFillerWords = false, DefaultTransform = null };

		string result = await Pipeline().ProcessAsync("raw", CancellationToken.None);

		result.Should().Be("normalized:raw");
		_cleaner.Received(1).Clean("raw", false);
	}

	[Fact]
	public async Task Applies_the_configured_default_transform_after_normalizing()
	{
		_holder.Current = new PostProcessOptions { RemoveFillerWords = true, DefaultTransform = "polish" };
		_rephraseClient.RephraseAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(new ValueTask<RephraseResult>(RephraseResult.Rephrased("REWRITTEN")));

		string result = await Pipeline().ProcessAsync("raw", CancellationToken.None);

		result.Should().Be("REWRITTEN");
		await _rephraseClient.Received(1).RephraseAsync("normalized:raw", Arg.Any<string>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task An_unknown_default_transform_degrades_to_the_normalized_text()
	{
		_holder.Current = new PostProcessOptions { RemoveFillerWords = true, DefaultTransform = "sparkle" };

		string result = await Pipeline().ProcessAsync("raw", CancellationToken.None);

		result.Should().Be("normalized:raw");
		await _rephraseClient.DidNotReceive().RephraseAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
	}
}
