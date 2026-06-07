# Coding Standards

Project-wide conventions for the .NET 10 rewrite. This document grows as conventions are decided;
formatting itself is enforced by `.editorconfig` + `dotnet format` and is not restated here.

## CQRS (source-generated Mediator)

Every application request goes through the source-generated **Mediator** (martinothamar,
`Mediator.Abstractions` + `Mediator.SourceGenerator`) — **not** MediatR. There is no other in-process
dispatch mechanism; callers never `new` a handler.

House rules:

- **Use the custom markers, not raw `IRequest`.** Application requests implement `ICommand<T>`
  (state-changing) or `IQuery<T>` (read-only); handlers implement `ICommandHandler<TCommand, T>` or
  `IQueryHandler<TQuery, T>` (`src/Application/Interfaces/`). The command/query split is part of the
  type signature, not a naming convention — a query handler must not mutate state.
- **Requests are immutable records.** Commands and queries are `sealed record` types carrying only
  the data the handler needs.
- **Handlers orchestrate; they do not compute or do I/O.** A handler wires `Logic.*` behaviors and
  ports together. Business math lives in `Logic.*`; I/O lives behind a port. A handler that contains
  a loop of domain math, or touches the file system / network directly, is misplaced code.
- **Cross-cutting concerns are pipeline behaviors, not handler code.** Validation, logging, and
  similar concerns are `IPipelineBehavior<,>` implementations registered once (see
  `ValidationBehavior` below), never copy-pasted into handlers.
- **Handlers depend on abstractions only.** Inject Application-declared interfaces (ports, Logic
  abstractions) — never a concrete `Infrastructure` or `Logic.*` type. This keeps Application free of
  outward dependencies (enforced by `tests/Architecture.Tests`).

Example: `src/Application/Transcription/DeliverTranscriptionCommand.cs` +
`DeliverTranscriptionHandler.cs`, exercised end-to-end by the `@WHISPER-58` push-to-talk scenario.

## Validation (FluentValidation)

Request validation is done with **FluentValidation**, enforced centrally by the Mediator pipeline.

House rules:

- **Validate in the pipeline, never in the handler.** `ValidationBehavior<TMessage, TResponse>`
  (`src/Application/Behaviors/`) runs every registered `IValidator<T>` for a request *before* its
  handler executes and throws `ValidationException` on failure — so a handler can assume its input is
  already valid. Handlers must not re-check inputs that a validator owns.
- **One validator per request.** Each command/query that needs validation has a single
  `AbstractValidator<TRequest>`; validators are registered automatically by `AddApplication()`.
- **A request with no validator passes through.** Validation is opt-in per request type; do not add an
  empty validator just to have one.
- **Validators are pure.** Rules operate on the request data only — no I/O, no service calls. Anything
  requiring I/O to decide validity belongs in the handler against a port, not in a validator.

## Mapping (Riok.Mapperly)

Mapperly is the **only** mapping tool. All object-to-object mapping is compile-time generated — no
AutoMapper, no hand-written mapping loops, no runtime reflection.

House rules:

- **`[Mapper]` partial class only.** Every mapper is a `partial class` annotated `[Mapper]` with
  `partial` mapping method declarations; Mapperly generates the bodies at build time.
- **No `[UseMapper]`.** Implicit mapper composition is forbidden — it hides which mapper does what
  and makes mappings hard to follow. If a mapping needs a nested conversion, declare that mapping
  method on the same mapper.
- **Never mock a mapper.** A generated Mapperly mapper is fast, deterministic, and has no I/O, so
  mocking it adds nothing and hides real mapping bugs. Unit tests and BDD specs use the **real**
  mapper.
- **Keep mappings warning-clean.** Mapperly diagnostics (`RMG####`) must not fire — the build runs
  with `-warnaserror`. Align DTO and domain member names/types so no member is silently unmapped.

Example: see `src/Application/Mapping/WhisperModelMapper.cs` and its round-trip test
`tests/Application.Tests/WhisperModelMapperTests.cs`.
