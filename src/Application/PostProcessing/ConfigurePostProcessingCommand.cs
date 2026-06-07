// CQRS command to change the post-process configuration (WHISPER-41). Carries the full desired
// configuration; returns Unit. Validation (FluentValidation in the ValidationBehavior pipeline) runs
// before the handler, so an invalid configuration (unknown default transform, non-loopback endpoint)
// never reaches the live holder.

using Application.Interfaces;

namespace Application.PostProcessing;

public sealed record ConfigurePostProcessingCommand(
	bool RemoveFillerWords,
	IReadOnlyList<string> CustomVocabulary,
	string? DefaultTransform,
	bool RephraseEnabled,
	string RephraseEndpoint) : ICommand<Mediator.Unit>;
