// Handles SwitchActiveModelCommand (WHISPER-27): switches the model lifecycle to the requested model,
// which releases the currently loaded model and loads the new one. The id has already passed the
// validator (a known catalog model), and the picker guarantees it is downloaded, so this is pure
// delegation to the lifecycle policy.

using Application.Interfaces;
using Application.Ports;

namespace Application.Models;

public sealed class SwitchActiveModelHandler(IModelLifecycle lifecycle)
	: ICommandHandler<SwitchActiveModelCommand, Mediator.Unit>
{
	public async ValueTask<Mediator.Unit> Handle(SwitchActiveModelCommand command, CancellationToken cancellationToken)
	{
		await lifecycle.SwitchAsync(command.ModelId, cancellationToken);
		return Mediator.Unit.Value;
	}
}
