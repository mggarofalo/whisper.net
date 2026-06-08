// CQRS command to download a model into the local cache (WHISPER-27). This is the one model-related
// network egress and it is always user-initiated — nothing dispatches it automatically. Carries the
// model id and an optional progress sink the picker's ViewModel owns, so live byte/percent progress
// flows back to the UI while the ViewModel still talks only through Mediator. Returns Unit; failure
// surfaces as an exception the caller turns into a terminal "failed" state.

using Application.Interfaces;
using Domain.Models;

namespace Application.Models;

public sealed record DownloadModelCommand(string ModelId, IProgress<ModelDownloadProgress>? Progress)
	: ICommand<Mediator.Unit>;
