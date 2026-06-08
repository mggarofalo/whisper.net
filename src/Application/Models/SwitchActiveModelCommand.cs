// CQRS command to make a downloaded model the active one (WHISPER-27): the picker dispatches it when the
// user selects a model. Carries the target id; the handler switches the model lifecycle to it (releasing
// the previous model first). Returns Unit. The picker downloads an un-cached model before dispatching
// this, so the switch always targets a model already on disk.

using Application.Interfaces;

namespace Application.Models;

public sealed record SwitchActiveModelCommand(string ModelId) : ICommand<Mediator.Unit>;
