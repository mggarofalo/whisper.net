# Spike: Whisper.net + Vulkan feasibility on the RTX 5080 (WHISPER-65)

**Decision: GO for Module 3 with Whisper.net + the Vulkan runtime.**

Time-boxed spike to de-risk Module 3 before committing to it. The Python predecessor's CUDA path
hung (CUDA-toolkit version mismatch on the RTX 5080); the hypothesis was that the **Vulkan** runtime
— which needs no CUDA toolkit, only the GPU driver's Vulkan loader — gives a working GPU path. It
does.

## Environment

- GPU: **NVIDIA GeForce RTX 5080** (driver 32.0.15.9649); also an AMD Radeon iGPU present.
- `vulkan-1.dll` present in `System32` (the Vulkan loader ships with the NVIDIA driver).
- OS: Windows 11; .NET SDK 10.0.108.

## Packages (pinned in the probe)

| Package | Version |
|---|---|
| `Whisper.net` | 1.9.1 |
| `Whisper.net.Runtime` (CPU) | 1.9.1 |
| `Whisper.net.Runtime.Vulkan` | 1.9.1 |

Model: `ggml-base.en` (147 MB), fetched once via `WhisperGgmlDownloader.Default.GetGgmlModelAsync`.
Audio: the canonical whisper.cpp `jfk.wav` (11 s, 16 kHz mono).

## What was tested

The disposable probe (`spikes/VulkanProbe`) runs in two modes by setting
`RuntimeOptions.RuntimeLibraryOrder` **before** the first factory load:

- `gpu` → `[Vulkan, Cpu]` (prefer GPU, fall back to CPU)
- `cpu` → `[Cpu]` (force CPU only — simulates no compatible GPU runtime present)

Each mode runs two transcription passes so one-time warmup is separated from steady state.

## Results

### 1. Vulkan transcribes correctly and the GPU is genuinely engaged

`RuntimeOptions.LoadedLibrary` reported **`Vulkan`**, and the native ggml logs confirm the RTX 5080
is the active backend (not a silent CPU fallback):

```
whisper_init_with_params_no_state: use gpu    = 1
ggml_vulkan: Found 2 Vulkan devices:
ggml_vulkan: 0 = NVIDIA GeForce RTX 5080 (NVIDIA) | fp16: 1 | bf16: 1 | matrix cores: NV_coopmat2
whisper_backend_init_gpu: using Vulkan0 backend
```

Transcript (both backends, identical and correct):
> "And so my fellow Americans, ask not what your country can do for you, ask what you can do for your country."

### 2. Timing — GPU vs CPU on the same clip

| Backend | First-ever run (cold) | Steady state (warm) |
|---|---|---|
| **Vulkan (RTX 5080)** | ~5,300 ms on the very first run, then **~160 ms** | **~132 ms** |
| CPU | ~734 ms | ~709 ms |

- The **~5.3 s on the very first run** is one-time Vulkan **shader/pipeline compilation**. The NVIDIA
  driver caches the compiled pipelines on disk, so every subsequent process launch is already fast
  (~160 ms cold). Within a process, a warmed factory transcribes the 11 s clip in **~132 ms**.
- Steady-state GPU is **~5× faster** than CPU (132 ms vs 709 ms) on `base.en`. The gap widens with
  larger models and longer audio; it shrinks (or inverts) for tiny models / very short clips where
  fixed overhead dominates.

### 3. CPU fallback works without error

Forcing `[Cpu]` loaded the CPU runtime (`LoadedLibrary = Cpu`; logs show `no GPU found`),
transcribed correctly, and surfaced no error — exactly the "no compatible GPU runtime" behavior the
user should never see fail.

## Implications for Module 3

- **Use Whisper.net 1.9.x with `Whisper.net.Runtime.Vulkan` + `Whisper.net.Runtime` (CPU).** Vulkan
  sidesteps the CUDA-toolkit hang entirely — it depends only on the driver's Vulkan loader.
- **Warm the factory once at startup and keep it alive.** Load the model / build the `WhisperFactory`
  during onboarding so the first user dictation doesn't eat the one-time ~5 s shader compile. Treat
  the factory as a long-lived singleton in `Logic.GpuContactPoint` / Infrastructure.
- **Keep CPU fallback first-class.** Configure `RuntimeLibraryOrder = [Vulkan, Cpu]`; if no GPU
  runtime loads, transcription must continue on CPU silently. `RuntimeOptions.LoadedLibrary` is the
  signal to surface (e.g. a tray tooltip "GPU"/"CPU"), not an error.
- **Multi-GPU note:** two Vulkan devices were enumerated (RTX 5080 at index 0, AMD iGPU at index 1).
  Device 0 was selected. Module 3 should expose GPU-device selection (or at least pin the
  discrete GPU) rather than assuming index 0 is always the dGPU.
- **Backend evidence for tests:** `RuntimeOptions.LoadedLibrary` and the `LogProvider` device lines
  give a clean, assertable signal that the GPU is engaged — usable in an Infrastructure integration
  test (kept out of the headless CI lane).

## Reproducing

```
cd spikes/VulkanProbe
dotnet run -- gpu   # prefer Vulkan; downloads jfk.wav + ggml-base.en once
dotnet run -- cpu   # force CPU-only fallback
```

> The probe is **disposable** and intentionally excluded from `Whisper.slnx` and CI. Downloaded
> artifacts (the model and `jfk.wav`) are gitignored. Delete `spikes/VulkanProbe` once Module 3 work
> begins; this document is the lasting record.
