// CQRS query for the model picker (WHISPER-27): lists every catalog model with its ratings, download
// state, and whether it is active. A read-only request carrying no data; the handler reads the on-device
// catalog, the cache, and the model lifecycle and projects to DTOs.

using Application.Interfaces;

namespace Application.Models;

public sealed record ListModelsQuery : IQuery<IReadOnlyList<ModelListItemDto>>;
