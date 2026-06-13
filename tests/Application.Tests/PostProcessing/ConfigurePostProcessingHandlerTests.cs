// Inner TDD loop for the post-process configuration handler: a validated command is
// applied to the live holder, so the pipeline picks up the change on the next transcription.

using Application.Configuration;
using Application.PostProcessing;
using Xunit;

namespace Application.Tests.PostProcessing;

public sealed class ConfigurePostProcessingHandlerTests
{
	[Fact]
	public async Task Applies_the_configuration_to_the_live_holder()
	{
		PostProcessSettingsHolder holder = new();
		ConfigurePostProcessingHandler handler = new(holder);

		await handler.Handle(
			new ConfigurePostProcessingCommand(
				RemoveFillerWords: false,
				CustomVocabulary: ["Reqnroll"],
				DefaultTransform: "polish",
				RephraseEnabled: true,
				RephraseEndpoint: "http://localhost:11434"),
			CancellationToken.None);

		Assert.False(holder.Current.RemoveFillerWords);
		Assert.Equal("Reqnroll", Assert.Single(holder.Current.CustomVocabulary));
		Assert.Equal("polish", holder.Current.DefaultTransform);
		Assert.True(holder.Current.RephraseEnabled);
	}
}
