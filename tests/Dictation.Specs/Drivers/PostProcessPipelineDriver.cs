// Drives the @WHISPER-41 scenarios against the REAL post-process pipeline (IPostProcessor) and the REAL
// ConfigurePostProcessing command flowing through IMediator (so the FluentValidation ValidationBehavior
// runs). The rephrase port is faked. Proves hot-reload (a mediated config change is applied on the next
// post-process call, same pipeline instance), the ordered normalize -> transform steps, and that an
// invalid configuration is reported while the pipeline still degrades safely.

using Application.Configuration;
using Application.Ports;
using Application.PostProcessing;
using Application.Rephrase;
using AwesomeAssertions;
using FluentValidation;
using Mediator;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class PostProcessPipelineDriver(
	IPostProcessor processor,
	IMediator mediator,
	PostProcessSettingsHolder holder,
	IRephraseClient rephraseClient)
{
	private string _result = string.Empty;
	private Exception? _error;

	public void FillerRemovalIsDisabled() => holder.Current = new PostProcessOptions { RemoveFillerWords = false };

	public Task EnableFillerRemoval() => SendConfigure(removeFillerWords: true, defaultTransform: null);

	public void RephraseRewritesTo(string rewritten) =>
		rephraseClient.RephraseAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(new ValueTask<RephraseResult>(RephraseResult.Rephrased(rewritten)));

	public Task ConfigureDefaultTransform(string name) => SendConfigure(removeFillerWords: true, defaultTransform: name);

	public Task ApplyConfiguration(string defaultTransform) => SendConfigure(removeFillerWords: true, defaultTransform: defaultTransform);

	public void LeavePipelineWithDefaultTransform(string name) =>
		holder.Current = new PostProcessOptions { RemoveFillerWords = true, DefaultTransform = name };

	public async Task PostProcess(string text) => _result = await processor.ProcessAsync(text, CancellationToken.None);

	public void AssertResult(string expected) => _result.Should().Be(expected);

	public void AssertValidationErrorAboutTransform()
	{
		_error.Should().BeOfType<ValidationException>();
		_error!.Message.ToLowerInvariant().Should().Contain("transform");
	}

	private async Task SendConfigure(bool removeFillerWords, string? defaultTransform)
	{
		try
		{
			await mediator.Send(new ConfigurePostProcessingCommand(
				removeFillerWords,
				CustomVocabulary: [],
				defaultTransform,
				RephraseEnabled: false,
				RephraseEndpoint: "http://localhost:11434"));
		}
		catch (Exception ex)
		{
			_error = ex;
		}
	}
}
