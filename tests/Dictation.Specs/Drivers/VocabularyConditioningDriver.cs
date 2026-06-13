// Drives the assembly scenarios against the REAL VocabularyConditioner resolved from DI.
// The conditioner is a pure function, so the driver simply feeds it a vocabulary and asserts on the
// assembled DecodingOptions — no model, no native library, no transcription.

using AwesomeAssertions;
using Domain.Models;
using Logic.ModelManagement;

namespace Dictation.Specs.Drivers;

public sealed class VocabularyConditioningDriver(VocabularyConditioner conditioner)
{
	private readonly List<string> _vocabulary = [];
	private DecodingOptions _options = DecodingOptions.Default;

	public void AddVocabularyTerms(params string[] terms) => _vocabulary.AddRange(terms);

	public void UseEmptyVocabulary() => _vocabulary.Clear();

	public void Assemble() => _options = conditioner.Assemble(_vocabulary);

	public void AssertInitialPromptIncludesGivenTerms()
	{
		_options.InitialPrompt.Should().NotBeNull();
		foreach (string term in _vocabulary)
		{
			_options.InitialPrompt.Should().Contain(term);
		}
	}

	public void AssertNoInitialPrompt() => _options.InitialPrompt.Should().BeNull();

	public void AssertFirstTokenThresholdDisabled() => _options.DisableFirstTokenLogProbThreshold.Should().BeTrue();

	public void AssertFirstTokenThresholdDefault() =>
		_options.DisableFirstTokenLogProbThreshold.Should().Be(DecodingOptions.Default.DisableFirstTokenLogProbThreshold);
}
