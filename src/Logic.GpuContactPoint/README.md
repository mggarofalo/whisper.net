# Logic.GpuContactPoint

The single, deliberate touch point for GPU-vs-CPU runtime policy — Vulkan availability and
fallback decisions. Pure logic; the native runtime probing lives in Infrastructure. Concentrating
GPU policy here keeps the rest of the codebase GPU-agnostic.

**Depends on:** Application, Domain.
