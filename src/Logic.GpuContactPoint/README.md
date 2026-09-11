# Logic.GpuContactPoint

The single, deliberate touch point for GPU-vs-CPU runtime policy — Vulkan availability and
fallback decisions. Pure logic; native runtime probing runs in the isolated inference worker. Concentrating
GPU policy here keeps the rest of the codebase GPU-agnostic.

**Depends on:** Application, Domain.
