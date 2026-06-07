// Inner TDD loop for the output-transforms registry (WHISPER-37): the built-in formats are present,
// resolution is case-insensitive, every built-in carries a non-empty prompt, and an unknown name does
// not resolve.

using AwesomeAssertions;
using Logic.AppManagement.OutputTransforms;
using Xunit;

namespace Logic.AppManagement.Tests.OutputTransforms;

public sealed class OutputTransformRegistryTests
{
	private readonly OutputTransformRegistry _registry = new();

	[Theory]
	[InlineData("bullets")]
	[InlineData("prompt-engineer")]
	[InlineData("polish")]
	public void Ships_the_built_in_transforms(string name)
	{
		_registry.TryResolve(name, out OutputTransform transform).Should().BeTrue();
		transform.Name.Should().Be(name);
		transform.Description.Should().NotBeNullOrWhiteSpace();
		transform.Prompt.Should().NotBeNullOrWhiteSpace();
	}

	[Fact]
	public void Resolves_case_insensitively()
	{
		_registry.TryResolve("BULLETS", out OutputTransform transform).Should().BeTrue();
		transform.Name.Should().Be("bullets");
	}

	[Fact]
	public void Does_not_resolve_an_unknown_name()
	{
		_registry.TryResolve("sparkle", out _).Should().BeFalse();
	}

	[Fact]
	public void Can_be_built_from_a_custom_catalog()
	{
		OutputTransformRegistry custom = new([new OutputTransform("shout", "Uppercase it.", "SHOUT: ")]);

		custom.TryResolve("shout", out _).Should().BeTrue();
		custom.TryResolve("bullets", out _).Should().BeFalse();
	}
}
