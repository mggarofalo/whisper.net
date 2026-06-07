# Application

Use-case orchestration. Holds the **ports** (interfaces the outer layers implement), the CQRS
messages (`ICommand<T>` / `IQuery<T>`), their handlers, pipeline behaviors, and DTOs.

**Depends on:** Domain only.
