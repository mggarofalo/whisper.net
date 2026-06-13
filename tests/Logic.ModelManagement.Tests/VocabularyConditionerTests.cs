// Inner TDD loop for VocabularyConditioner, exercised in isolation — no native model load.
// A non-empty vocabulary becomes a biasing initial prompt and disables the first-token threshold; an
// empty/blank vocabulary leaves decoding at its defaults; and assembly is pure (stateless across calls).

using AwesomeAssertions;
using Domain.Models;
using Xunit;

namespace Logic.ModelManagement.Tests;

public sealed class VocabularyConditionerTests
{
	private readonly VocabularyConditioner _conditioner = new();

	[Fact]
	public void Assembles_a_biasing_initial_prompt_from_the_vocabulary()
	{
		DecodingOptions options = _conditioner.Assemble(["Reqnroll", "Velopack"]);

		options.InitialPrompt.Should().NotBeNull();
		options.InitialPrompt.Should().Contain("Reqnroll").And.Contain("Velopack");
	}

	[Fact]
	public void Disables_the_first_token_threshold_when_a_vocabulary_is_present()
	{
		_conditioner.Assemble(["Reqnroll"]).DisableFirstTokenLogProbThreshold.Should().BeTrue();
	}

	[Fact]
	public void Leaves_decoding_unchanged_for_a_null_vocabulary()
	{
		_conditioner.Assemble(null).Should().Be(DecodingOptions.Default);
	}

	[Fact]
	public void Leaves_decoding_unchanged_for_an_empty_vocabulary()
	{
		_conditioner.Assemble([]).Should().Be(DecodingOptions.Default);
	}

	[Fact]
	public void Leaves_decoding_unchanged_for_an_all_blank_vocabulary()
	{
		_conditioner.Assemble(["   ", ""]).Should().Be(DecodingOptions.Default);
	}

	[Fact]
	public void Trims_terms_and_ignores_blanks()
	{
		DecodingOptions options = _conditioner.Assemble(["  Reqnroll  ", "   ", "Velopack"]);

		options.InitialPrompt.Should().Be("Reqnroll, Velopack");
	}

	[Fact]
	public void Is_stateless_so_a_changed_vocabulary_is_reflected_immediately()
	{
		_conditioner.Assemble(["Reqnroll"]);

		// A subsequent call with a different vocabulary is unaffected by the previous one (no engine state).
		_conditioner.Assemble(["Velopack"]).InitialPrompt.Should().Be("Velopack");
	}
}
