// Inner TDD loop for the model registry: it enumerates the supported Whisper variants with a
// canonical ggml file name and a positive size, and resolves ids case-insensitively (unknown ids yield
// null). The catalog is pure data, so these assert its shape, not behavior over a device.

using Application.Ports;
using AwesomeAssertions;
using Domain.Models;
using Xunit;

namespace Logic.ModelManagement.Tests;

public sealed class WhisperModelCatalogTests
{
	private readonly IModelCatalog _catalog = new WhisperModelCatalog();

	[Fact]
	public void Enumerates_the_common_whisper_variants()
	{
		IEnumerable<string> ids = _catalog.Entries.Select(entry => entry.Id);

		ids.Should().Contain(["tiny", "base", "small", "medium", "large-v3"]);
	}

	// The turbo builds are what a dictation user most often wants — near-large accuracy at a fraction of the
	// decode cost — and they were missing from the catalog entirely, so they could not be downloaded at all.
	[Fact]
	public void Offers_the_large_v3_turbo_builds()
	{
		IEnumerable<string> ids = _catalog.Entries.Select(entry => entry.Id);

		ids.Should().Contain(["large-v3-turbo", "large-v3-turbo-q5_0"]);
	}

	[Fact]
	public void Every_entry_has_a_ggml_file_name_and_a_positive_size()
	{
		foreach (WhisperModelCatalogEntry entry in _catalog.Entries)
		{
			entry.FileName.Should().Be($"ggml-{entry.Id}.bin");
			entry.SizeBytes.Should().BePositive();
			entry.DisplayName.Should().NotBeNullOrWhiteSpace();
		}
	}

	[Theory]
	[InlineData("base")]
	[InlineData("BASE")]
	[InlineData("Base")]
	public void Finds_a_known_model_case_insensitively(string id) =>
		_catalog.Find(id)!.Id.Should().Be("base");

	[Fact]
	public void Returns_null_for_an_unknown_model() =>
		_catalog.Find("does-not-exist").Should().BeNull();
}
