// Drives the output-transform scenarios against the REAL OutputTransformService + OutputTransformRegistry,
// with the rephrase port (IRephraseClient) faked so the test controls whether the AI backend is
// available, disabled, or never reached. Proves the prompt+text are composed and handed to the port,
// that an unknown transform is a recoverable error with no rephrase call, and that a disabled backend
// degrades gracefully.

using Application.Ports;
using Application.Rephrase;
using AwesomeAssertions;
using Logic.AppManagement.OutputTransforms;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class OutputTransformDriver(
	OutputTransformService service,
	IRephraseClient rephraseClient,
	OutputTransformRegistry registry)
{
	private string _appliedName = string.Empty;
	private string _appliedText = string.Empty;
	private TransformResult? _result;

	public void AssertTransformIsRegistered(string name) =>
		registry.TryResolve(name, out _).Should().BeTrue($"'{name}' should be a built-in transform");

	public void AssertTransformIsNotRegistered(string name) =>
		registry.TryResolve(name, out _).Should().BeFalse($"'{name}' should not be registered");

	public void RephraseClientIsAvailable() =>
		rephraseClient.RephraseAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(call => new ValueTask<RephraseResult>(RephraseResult.Rephrased($"REWRITTEN: {call.ArgAt<string>(0)}")));

	public void RephraseClientIsDisabled() =>
		rephraseClient.RephraseAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(call => new ValueTask<RephraseResult>(RephraseResult.Disabled(call.ArgAt<string>(0))));

	public async Task Apply(string name, string text)
	{
		_appliedName = name;
		_appliedText = text;
		_result = await service.ApplyAsync(name, text, CancellationToken.None);
	}

	public void AssertRephraseReceivedTransformPrompt()
	{
		registry.TryResolve(_appliedName, out OutputTransform transform).Should().BeTrue();
		rephraseClient.Received(1).RephraseAsync(_appliedText, transform.Prompt, Arg.Any<CancellationToken>());
	}

	public void AssertRewrittenResultReturned()
	{
		_result!.Status.Should().Be(TransformStatus.Applied);
		_result.Text.Should().Be($"REWRITTEN: {_appliedText}");
	}

	public void AssertUnknownTransformError() => _result!.Status.Should().Be(TransformStatus.UnknownTransform);

	public void AssertNoRephraseCall() =>
		rephraseClient.DidNotReceive().RephraseAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

	public void AssertReturnedUnchanged(string expected)
	{
		_result!.Status.Should().Be(TransformStatus.Disabled);
		_result.Text.Should().Be(expected);
	}
}
