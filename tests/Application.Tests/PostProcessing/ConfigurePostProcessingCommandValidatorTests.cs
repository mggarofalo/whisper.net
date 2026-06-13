// Inner TDD loop for the post-process configuration validator: a known (or empty) default
// transform is accepted, an unknown one is rejected, and an enabled rephrase must be loopback-only.

using Application.PostProcessing;
using FluentValidation.Results;
using Xunit;

namespace Application.Tests.PostProcessing;

public sealed class ConfigurePostProcessingCommandValidatorTests
{
	private readonly ConfigurePostProcessingCommandValidator _validator = new();

	private static ConfigurePostProcessingCommand Command(
		string? defaultTransform = null,
		bool rephraseEnabled = false,
		string endpoint = "http://localhost:11434") =>
		new(RemoveFillerWords: true, CustomVocabulary: [], defaultTransform, rephraseEnabled, endpoint);

	[Theory]
	[InlineData("bullets")]
	[InlineData("prompt-engineer")]
	[InlineData("polish")]
	[InlineData(null)]
	[InlineData("")]
	public void Accepts_a_known_or_unset_default_transform(string? transform)
	{
		Assert.True(_validator.Validate(Command(defaultTransform: transform)).IsValid);
	}

	[Fact]
	public void Rejects_an_unknown_default_transform()
	{
		ValidationResult result = _validator.Validate(Command(defaultTransform: "sparkle"));

		Assert.False(result.IsValid);
		Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("transform"));
	}

	[Fact]
	public void Rejects_a_non_loopback_rephrase_endpoint_when_enabled()
	{
		ValidationResult result = _validator.Validate(Command(rephraseEnabled: true, endpoint: "http://ollama.example.com"));

		Assert.False(result.IsValid);
		Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("localhost only"));
	}

	[Fact]
	public void Accepts_a_loopback_rephrase_endpoint_when_enabled()
	{
		Assert.True(_validator.Validate(Command(rephraseEnabled: true, endpoint: "http://127.0.0.1:11434")).IsValid);
	}

	[Fact]
	public void Ignores_the_endpoint_when_rephrase_is_disabled()
	{
		Assert.True(_validator.Validate(Command(rephraseEnabled: false, endpoint: "http://ollama.example.com")).IsValid);
	}
}
