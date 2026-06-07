// Handles ConfigurePostProcessingCommand: the command has already passed the ValidationBehavior
// pipeline, so the handler trusts its input and applies it to the live holder. The pipeline reads the
// holder on the next transcription, so the change takes effect without restarting the app.

using Application.Configuration;
using Application.Interfaces;

namespace Application.PostProcessing;

public sealed class ConfigurePostProcessingHandler(PostProcessSettingsHolder holder)
	: ICommandHandler<ConfigurePostProcessingCommand, Mediator.Unit>
{
	public ValueTask<Mediator.Unit> Handle(ConfigurePostProcessingCommand command, CancellationToken cancellationToken)
	{
		holder.Current = new PostProcessOptions
		{
			RemoveFillerWords = command.RemoveFillerWords,
			CustomVocabulary = [.. command.CustomVocabulary],
			DefaultTransform = command.DefaultTransform,
			RephraseEnabled = command.RephraseEnabled,
			RephraseEndpoint = command.RephraseEndpoint,
		};

		return ValueTask.FromResult(Mediator.Unit.Value);
	}
}
