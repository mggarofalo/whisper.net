// Handles DownloadModelCommand (WHISPER-27): resolves the catalog entry for the requested id and runs
// the downloader, forwarding the caller's progress sink. The id has already passed the validator (so it
// is a known model), and the downloader verifies integrity and leaves the file in the cache before this
// returns. The one user-initiated model network egress lives behind this single seam.

using Application.Interfaces;
using Application.Ports;
using Domain.Models;

namespace Application.Models;

public sealed class DownloadModelHandler(IModelCatalog catalog, IModelDownloader downloader)
	: ICommandHandler<DownloadModelCommand, Mediator.Unit>
{
	public async ValueTask<Mediator.Unit> Handle(DownloadModelCommand command, CancellationToken cancellationToken)
	{
		WhisperModelCatalogEntry entry = catalog.Find(command.ModelId)
			?? throw new ModelNotFoundException(command.ModelId);

		await downloader.DownloadAsync(entry, command.Progress, cancellationToken);
		return Mediator.Unit.Value;
	}
}
