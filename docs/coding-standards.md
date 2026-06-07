# Coding Standards

Project-wide conventions for the .NET 10 rewrite. This document grows as conventions are decided;
formatting itself is enforced by `.editorconfig` + `dotnet format` and is not restated here.

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
