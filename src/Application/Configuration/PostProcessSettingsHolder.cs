// The live, in-memory view of the post-process configuration, mirroring the SettingsHolder
// pattern. Seeded from configuration on startup and replaced when the user reconfigures
// post-processing; the pipeline reads Current on each transcription, so an edit is picked up on the next
// utterance without restarting the app.

namespace Application.Configuration;

public sealed class PostProcessSettingsHolder
{
	public PostProcessOptions Current { get; set; } = PostProcessOptions.Default;
}
