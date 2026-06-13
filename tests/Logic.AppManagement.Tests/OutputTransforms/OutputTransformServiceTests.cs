// Inner TDD loop for the output-transforms service: applying a known transform composes
// its prompt with the text and returns the rephrased result; an unknown name is a recoverable error
// with no rephrase call; and a disabled or failed rephrase backend degrades to the original text.

using Application.Ports;
using Application.Rephrase;
using AwesomeAssertions;
using Logic.AppManagement.OutputTransforms;
using NSubstitute;
using Xunit;

namespace Logic.AppManagement.Tests.OutputTransforms;

public sealed class OutputTransformServiceTests
{
	private readonly IRephraseClient _rephraseClient = Substitute.For<IRephraseClient>();
	private readonly OutputTransformRegistry _registry = new();

	private OutputTransformService Service() => new(_registry, _rephraseClient);

	[Fact]
	public async Task Applies_a_known_transform_by_composing_its_prompt_and_returning_the_rewrite()
	{
		_rephraseClient.RephraseAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(new ValueTask<RephraseResult>(RephraseResult.Rephrased("- milk\n- eggs")));
		_registry.TryResolve("bullets", out OutputTransform bullets);

		TransformResult result = await Service().ApplyAsync("bullets", "milk and eggs", CancellationToken.None);

		result.Status.Should().Be(TransformStatus.Applied);
		result.Text.Should().Be("- milk\n- eggs");
		await _rephraseClient.Received(1).RephraseAsync("milk and eggs", bullets.Prompt, Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task An_unknown_transform_is_recoverable_and_makes_no_rephrase_call()
	{
		TransformResult result = await Service().ApplyAsync("sparkle", "some text", CancellationToken.None);

		result.Status.Should().Be(TransformStatus.UnknownTransform);
		result.Text.Should().Be("some text");
		await _rephraseClient.DidNotReceive().RephraseAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task A_disabled_backend_degrades_to_the_original_text()
	{
		_rephraseClient.RephraseAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(call => new ValueTask<RephraseResult>(RephraseResult.Disabled(call.ArgAt<string>(0))));

		TransformResult result = await Service().ApplyAsync("polish", "leave me", CancellationToken.None);

		result.Status.Should().Be(TransformStatus.Disabled);
		result.Text.Should().Be("leave me");
	}

	[Fact]
	public async Task A_failed_backend_degrades_to_the_original_text()
	{
		_rephraseClient.RephraseAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(call => new ValueTask<RephraseResult>(RephraseResult.Failed(call.ArgAt<string>(0))));

		TransformResult result = await Service().ApplyAsync("polish", "leave me", CancellationToken.None);

		result.Status.Should().Be(TransformStatus.Failed);
		result.Text.Should().Be("leave me");
	}
}
