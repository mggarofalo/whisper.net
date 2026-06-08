// Handles CopyToClipboardCommand (WHISPER-45): writes the given text to the system clipboard through the
// IClipboard port. The clipboard call is a fast, synchronous local OS operation, so the handler simply
// invokes it and completes.

using Application.Interfaces;
using Application.Ports;

namespace Application.History;

public sealed class CopyToClipboardHandler(IClipboard clipboard)
	: ICommandHandler<CopyToClipboardCommand, Mediator.Unit>
{
	public ValueTask<Mediator.Unit> Handle(CopyToClipboardCommand command, CancellationToken cancellationToken)
	{
		clipboard.SetText(command.Text);
		return ValueTask.FromResult(Mediator.Unit.Value);
	}
}
