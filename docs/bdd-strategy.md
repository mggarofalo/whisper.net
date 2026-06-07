# BDD Strategy

A concrete, opinionated Behavior-Driven Development strategy for the new **.NET 10 WPF dictation utility** (tray-resident, Clean Architecture + CQRS via source-generated `Mediator`).

Audience: an experienced .NET engineer who has never built a BDD project before. Read it top to bottom once; then keep section 8 (adoption path) and the Module 0 checklist open while you wire up the harness.

> **Scope note.** This document describes the *target* .NET 10 application. The current repository (`whisper-local`) is the Python predecessor; this file is the design record for the rewrite's test strategy.

---

## 0. The fixed stack (context, not up for debate here)

- **App:** WPF tray-resident dictation utility, **.NET 10** (LTS, GA 2025-11-11, supported through 2028-11-10).
- **Architecture:** Clean Architecture + CQRS. Source-generated `Mediator` (martinothamar) — **not** MediatR — with custom `ICommand<T>` / `IQuery<T>` marker interfaces.
- **Mapping:** `Riok.Mapperly`. **Composition:** Generic Host + Microsoft DI + Serilog.
- **Layering:**
  `Domain` (no deps) ← `Application` (ports/interfaces + Mediator handlers + DTOs) ← `Logic.*` (`Logic.AppManagement`, `Logic.AudioManagement`, `Logic.ModelManagement`, `Logic.GpuContactPoint`) ← `Infrastructure` (implements ports: Whisper.net, NAudio, ONNX VAD, SendInput, Ollama HttpClient, persistence) ← `Presentation` (WPF + MVVM via CommunityToolkit.Mvvm).
- **Test stack:** xUnit + **Reqnroll** (BDD) + NSubstitute (mocking) + AwesomeAssertions (assertions) + coverlet/ReportGenerator (coverage).

**Team commitments this strategy must honor:**
- BDD + TDD together; every requirement governed by Gherkin.
- Definition of Done includes validating acceptance criteria **and** behavioral descriptions.
- Coverage ~80% as a **guideline, not a gate**.
- Prefer **no test** over a meaningless box-checking test.
- UI is intentionally **iterative**, not waterfalled.

---

## 1. Mental model: what BDD actually is

### BDD is a conversation technique that happens to leave executable artifacts

The single most common beginner mistake is believing **"BDD = tests written with Given/When/Then."** That is the *residue* of BDD, not BDD itself. BDD is a practice for **discovering and agreeing on behavior in business language before you write code**, using concrete examples. The Gherkin `.feature` files are the written record of that agreement; making them executable is a bonus that keeps the record honest over time.

If you write Gherkin *after* the code, alone, to satisfy a coverage rule, you have a verbose unit-test framework and none of the benefit. Treat a `.feature` file as a **specification you could hand to a non-programmer** (you, three months later, counts as a non-programmer).

Three pillars to internalize:

1. **Ubiquitous language.** The words in your scenarios are the words in your domain model. If the spec says "trailing silence" and "push-to-talk," your `Domain` and `Application` types use those words. A scenario that uses different vocabulary than the code is a smell — usually it means the model is wrong, or the scenario is describing mechanics.
2. **Specification by example.** You don't say "the system handles silence correctly." You give a concrete example: *given a clip with 800 ms of trailing silence and a 500 ms threshold, the delivered text comes from the first portion and the trailing silence is trimmed.* Examples are unambiguous; abstractions hide disagreement.
3. **Outside-in.** You start from observable behavior (a command/query result), not from a class you feel like writing. The scenario is the "outside"; TDD drives the "inside."

### The double loop: how BDD and TDD compose

BDD and TDD are not competitors. They are nested loops.

```
OUTER LOOP  (BDD — behavior, hours/days)
┌────────────────────────────────────────────────────────────┐
│ 1. Write a failing acceptance scenario (.feature, Gherkin)   │
│    → expresses ONE behavior in ubiquitous language           │
│                                                              │
│    INNER LOOP (TDD — implementation, minutes)                │
│    ┌──────────────────────────────────────────────┐          │
│    │ a. Write a failing xUnit unit test (RED)       │          │
│    │ b. Write the minimum code to pass (GREEN)      │          │
│    │ c. Refactor (still GREEN)                      │          │
│    │ repeat until the handler/domain logic exists   │          │
│    └──────────────────────────────────────────────┘          │
│                                                              │
│ 2. Scenario goes GREEN → behavior is done                    │
│ 3. Refactor across units, scenario stays GREEN               │
└────────────────────────────────────────────────────────────┘
```

Concretely for this app: you pick a behavior ("transcription is delivered to the focused field on push-to-talk release"), write the Gherkin, write the step definitions that send a `DeliverTranscriptionCommand` through `IMediator`, and watch it fail because there's no handler. *Then* you drop into xUnit + TDD to build the handler and the `Logic.*` pieces it needs, red-green-refactor. When the unit work is done, the Gherkin scenario turns green and the outer loop closes.

**Division of labor:**
- **Gherkin/acceptance** answers *"are we building the right thing?"* (behavior, orchestration, domain rules).
- **xUnit/unit** answers *"are we building the thing right?"* (edge cases, value objects, pure functions, branch coverage).

The acceptance scenario is the *spec*; the unit tests are the *scaffolding* you build to satisfy it. You will write **far more** xUnit tests than scenarios. That ratio is correct and healthy.

---

## 2. Reqnroll setup for this stack (verified)

### Why Reqnroll, and the SpecFlow EOL fact

**SpecFlow is end-of-life.** Tricentis announced the open-source project's EOL in December 2024; it reached EOL on **2024-12-31**, the GitHub repos were deleted, and support was disabled as of 2025-01-01. ([Reqnroll: SpecFlow EOL announced](https://reqnroll.net/news/2025/01/specflow-end-of-life-has-been-announced/))

**Reqnroll is the maintained successor.** It is a community fork of SpecFlow (the SpecFlow name is a Tricentis trademark, hence the rename), created in early 2024 by Gáspár Nagy (SpecFlow's original creator) and actively developed, with .NET 8/9/10 support and 5000+ projects on it by early 2025. Migration from SpecFlow is largely find-and-replace. ([Reqnroll EOL post](https://reqnroll.net/news/2025/01/specflow-end-of-life-has-been-announced/); [SeanKilleen overview](https://seankilleen.com/2025/01/farewell-specflow-gaspar-nagy-saves-the-day-with-reqnroll/)) For a greenfield project there is no migration — just start on Reqnroll.

### Verified packages and versions (as of 2026-06)

| Package | Version | Notes |
|---|---|---|
| `Reqnroll.xunit.v3` | **3.3.4** | Use the **v3** package — xUnit v3 is the current line and what new projects should target. Published 2026-03-23; targets `netstandard2.0`, compatible with .NET 10. ([NuGet](https://www.nuget.org/packages/Reqnroll.xunit.v3/)) |
| `Reqnroll.Microsoft.Extensions.DependencyInjection` | **3.3.4** | The DI plugin that lets step definitions resolve from a `Microsoft.Extensions.DependencyInjection` container — i.e. the *same* container your Generic Host uses. Requires `Microsoft.Extensions.DependencyInjection` v6+. ([NuGet](https://www.nuget.org/packages/Reqnroll.Microsoft.Extensions.DependencyInjection); [docs](https://docs.reqnroll.net/latest/integrations/dependency-injection.html)) |

> **xUnit v2 vs v3.** `Reqnroll.xUnit` (3.3.x) targets xUnit v2; `Reqnroll.xunit.v3` targets xUnit v3. xUnit v3 support landed in Reqnroll v3.1 (2025-09-29). New project → **`Reqnroll.xunit.v3`**, and use `xunit.v3` runner packages throughout the test stack so versions don't split. ([Reqnroll v3.1 release](https://reqnroll.net/news/2025/09/reqnroll-v3-1-released/))

### Living documentation / reporting successor

SpecFlow+ LivingDoc was proprietary and could not be forked. Reqnroll's answer is **native HTML report generation**, introduced in **V3**, built on the standard **Cucumber Messages** format + the open-source Cucumber HTML Formatter — free, open source, no external conversion tool. ([Reqnroll roadmap: HTML report](https://reqnroll.net/news/2025/06/roadmap-update-html-report/)) Third-party options also integrate (Allure, Expressium LivingDoc) if you outgrow the built-in report. ([Living Documentation discussion](https://github.com/orgs/reqnroll/discussions/196))

Enable the native HTML report in `reqnroll.json` via a `formatters` section.

### Minimal `Dictation.Specs.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <!-- Required for xUnit v3 -->
    <OutputType>Exe</OutputType>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Reqnroll.xunit.v3" Version="3.3.4" />
    <PackageReference Include="Reqnroll.Microsoft.Extensions.DependencyInjection" Version="3.3.4" />
    <PackageReference Include="xunit.v3" Version="*" />            <!-- pin to current -->
    <PackageReference Include="xunit.runner.visualstudio" Version="*" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="*" />
    <PackageReference Include="NSubstitute" Version="*" />
    <PackageReference Include="AwesomeAssertions" Version="*" />
    <PackageReference Include="coverlet.collector" Version="*" />
  </ItemGroup>

  <ItemGroup>
    <!-- Drive behavior headlessly: reference everything EXCEPT Presentation. -->
    <ProjectReference Include="..\..\src\Domain\Domain.csproj" />
    <ProjectReference Include="..\..\src\Application\Application.csproj" />
    <ProjectReference Include="..\..\src\Logic.AppManagement\Logic.AppManagement.csproj" />
    <ProjectReference Include="..\..\src\Logic.AudioManagement\Logic.AudioManagement.csproj" />
    <ProjectReference Include="..\..\src\Logic.ModelManagement\Logic.ModelManagement.csproj" />
    <ProjectReference Include="..\..\src\Logic.GpuContactPoint\Logic.GpuContactPoint.csproj" />
    <!-- Infrastructure referenced ONLY so its port interfaces exist; real impls are
         replaced by NSubstitute fakes in the DI registration. Prefer NOT referencing
         Infrastructure at all if ports live in Application. -->
  </ItemGroup>
</Project>
```

### Minimal `reqnroll.json`

```json
{
  "$schema": "https://schemas.reqnroll.net/reqnroll-config-latest.json",
  "language": {
    "feature": "en-US"
  },
  "bindingCulture": {
    "name": "en-US"
  },
  "formatters": {
    "html": {
      "outputFilePath": "TestResults/living-doc.html"
    }
  }
}
```

That `formatters.html` block is the native living-documentation output — it regenerates on every test run with zero extra tooling. ([Reqnroll roadmap: HTML report](https://reqnroll.net/news/2025/06/roadmap-update-html-report/))

### Wiring the DI plugin to the app's real container

The whole point: step definitions should drive **real handlers** through the **same composition** the app uses, with only the Infrastructure ports faked. The DI plugin exposes a `[ScenarioDependencies]` static factory returning an `IServiceCollection`; the plugin creates a fresh scope per scenario and auto-registers `[Binding]` classes. ([docs](https://docs.reqnroll.net/latest/integrations/dependency-injection.html))

```csharp
// Support/TestDependencies.cs
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Reqnroll.Microsoft.Extensions.DependencyInjection;

public static class TestDependencies
{
    [ScenarioDependencies]
    public static IServiceCollection CreateServices()
    {
        var services = new ServiceCollection();

        // 1. Register the REAL application surface exactly as the host does.
        //    Reuse the same extension methods the WPF host calls so specs can't
        //    drift from production composition.
        services.AddApplication();      // Mediator handlers, pipeline behaviors, Mapperly
        services.AddLogicServices();    // Logic.* behaviors (real)

        // 2. Replace Infrastructure PORTS with NSubstitute fakes.
        //    These are the only seams the specs control.
        services.AddSingleton(_ => Substitute.For<ITranscriber>());
        services.AddSingleton(_ => Substitute.For<IAudioSource>());
        services.AddSingleton(_ => Substitute.For<ITextInjector>());   // wraps SendInput
        services.AddSingleton(_ => Substitute.For<IGpuRuntimeProbe>());

        // 3. A shared, scenario-scoped context object for capturing results.
        services.AddScoped<ScenarioWorld>();

        return services;
    }
}
```

Because `AddApplication()` / `AddLogicServices()` are the *same* registration methods Presentation calls, your scenarios exercise production composition — pipeline behaviors, validation, mapping — not a parallel test wiring that can lie.

---

## 3. Where specs live in a Clean Architecture solution

### Principle: scenarios drive behavior headlessly through `IMediator`

A `.feature` scenario should **never** click a WPF button. It should send an `ICommand<T>` / `IQuery<T>` through `IMediator` and assert on the result and on fake-port interactions. This keeps scenarios:

- fast (no UI thread, no real audio device, no real model),
- deterministic (fakes return canned data),
- focused on **behavior** (orchestration + domain rules) rather than rendering.

### Recommended solution placement

```
tests/
└── Dictation.Specs/                 # the acceptance/BDD project
    ├── Dictation.Specs.csproj        # references Domain + Application + Logic.* ; NOT Presentation
    ├── reqnroll.json
    ├── Features/                      # .feature files, grouped by capability
    │   ├── PushToTalk/
    │   │   └── DeliverOnRelease.feature
    │   ├── Vad/
    │   │   └── TrimTrailingSilence.feature
    │   ├── TextProcessing/
    │   │   └── FillerWordRemoval.feature
    │   └── ModelManagement/
    │       └── CpuFallback.feature
    ├── StepDefinitions/               # thin bindings: parse Gherkin → call a Driver
    │   ├── PushToTalkSteps.cs
    │   ├── VadSteps.cs
    │   └── ...
    ├── Drivers/                       # the real work: send IMediator commands, set up fakes
    │   ├── TranscriptionDriver.cs
    │   ├── VadDriver.cs
    │   └── AudioFixtureDriver.cs
    ├── Support/                       # cross-cutting test infra
    │   ├── TestDependencies.cs        # [ScenarioDependencies] DI factory
    │   ├── Hooks.cs                   # [BeforeScenario]/[AfterScenario]
    │   └── ScenarioWorld.cs           # scenario-scoped captured state
    └── Fixtures/
        └── wav/
            ├── hello-world-800ms-silence.wav
            └── ...
```

Plain xUnit unit tests live **separately**, next to the code they cover:

```
tests/
├── Domain.Tests/
├── Application.Tests/
├── Logic.AudioManagement.Tests/
└── ...
```

Don't mix Gherkin and unit tests in one project. Different cadence, different audience, different naming conventions.

### The Driver pattern (the Page-Object equivalent for a headless app)

In UI BDD you use Page Objects so step definitions don't know about CSS selectors. Headless, the equivalent is a **Driver**: a class that owns *how* to invoke a behavior, so the step definition only describes *what* behavior. **Step definitions stay thin; Drivers hold the mechanics.**

```csharp
// Drivers/TranscriptionDriver.cs
public sealed class TranscriptionDriver
{
    private readonly IMediator _mediator;
    private readonly ITranscriber _transcriber;   // the NSubstitute fake
    private readonly ITextInjector _injector;      // the NSubstitute fake
    private readonly ScenarioWorld _world;

    public TranscriptionDriver(
        IMediator mediator,
        ITranscriber transcriber,
        ITextInjector injector,
        ScenarioWorld world)
    {
        _mediator = mediator;
        _transcriber = transcriber;
        _injector = injector;
        _world = world;
    }

    public void GivenTheModelWillTranscribeTo(string text) =>
        _transcriber.TranscribeAsync(Arg.Any<AudioClip>(), Arg.Any<CancellationToken>())
                    .Returns(new TranscriptionResult(text));

    public async Task WhenPushToTalkIsReleased() =>
        _world.LastResult = await _mediator.Send(new DeliverTranscriptionCommand(_world.CapturedClip));

    public void ThenTextDeliveredToFocusedFieldWas(string expected) =>
        _injector.Received(1).Inject(expected);
}
```

The step definition becomes a one-liner per step, which is the whole goal:

```csharp
[Binding]
public sealed class PushToTalkSteps
{
    private readonly TranscriptionDriver _driver;
    public PushToTalkSteps(TranscriptionDriver driver) => _driver = driver; // injected by DI plugin

    [Given(@"the model will transcribe the audio to ""(.*)""")]
    public void GivenModelTranscribesTo(string text) => _driver.GivenTheModelWillTranscribeTo(text);

    [When(@"push-to-talk is released")]
    public Task WhenReleased() => _driver.WhenPushToTalkIsReleased();

    [Then(@"the text delivered to the focused field is ""(.*)""")]
    public void ThenDelivered(string text) => _driver.ThenTextDeliveredToFocusedFieldWas(text);
}
```

Rule of thumb: **a step definition that contains business logic, loops, or more than ~3 lines belongs in a Driver.**

---

## 4. Gherkin authoring standards

### Declarative over imperative

Describe **what** behavior is expected, not the **clicks/keystrokes** that produce it. Imperative scenarios are brittle and unreadable; declarative ones survive refactoring.

| Style | Example step |
|---|---|
| ❌ Imperative | `When I press and hold the Ctrl+Win key for 1200 ms then release it` |
| ✅ Declarative | `When I dictate "schedule the meeting"` |

### Core rules

- **One behavior per scenario.** If the title needs "and," split it.
- **`Background`** for shared `Given`s — but only context that's truly common; over-stuffed backgrounds hide what a scenario depends on.
- **`Scenario Outline` + `Examples`** for the *same behavior* across varying data (thresholds, filler words). Don't use an Outline to smush *different* behaviors together.
- **Tags** for slicing and traceability:
  - `@wip` — work in progress, excluded from the CI gate.
  - `@slow` — anything touching a real model/IO fixture you want to filter.
  - `@WHISPER-123` — the **Plane issue id**, so every scenario traces to a requirement (section 7).
- **No UI/implementation coupling.** No widget names, no key codes, no "the SendInput call." Talk about *dictation*, *delivery*, *silence*, *filler words*.
- **No incidental detail.** Only include data that affects the outcome. If the exact silence duration matters, state it; if it doesn't, don't.
- **Ubiquitous language.** The nouns/verbs match `Domain`/`Application` types: `TranscriptionResult`, "trailing silence," "push-to-talk release," "CPU fallback."

### GOOD vs BAD examples from this app

**(a) Push-to-talk delivery**

```gherkin
# ❌ BAD — imperative, UI-coupled, multiple behaviors, incidental detail
Scenario: User records and gets text
  Given the app tray icon is green
  And the microphone device "Realtek (3- High Definition Audio)" is selected
  When I press Ctrl+Win and hold for 1.2 seconds
  And I speak into the microphone
  And I release the keys after exactly 1200 ms
  And the spinner stops
  Then the SendInput API is invoked with the transcribed characters
  And the tray icon turns blue again
```

```gherkin
# ✅ GOOD — declarative, one behavior, ubiquitous language
@WHISPER-101
Scenario: Transcription is delivered to the focused field on push-to-talk release
  Given the model will transcribe the audio to "schedule the meeting"
  When push-to-talk is released
  Then the text delivered to the focused field is "schedule the meeting"
```

**(b) VAD silence gating**

```gherkin
# ❌ BAD — tests the algorithm's internals, not the behavior
Scenario: VAD
  Given a 16kHz mono PCM buffer of 48000 samples
  When the ONNX VAD model returns probabilities below 0.5 for frames 75..150
  Then the segment boundary index is 74
```

```gherkin
# ✅ GOOD — observable behavior, data-driven, declarative
@WHISPER-114
Scenario Outline: Trailing silence beyond the threshold is trimmed before delivery
  Given a recording of "<spoken>" followed by <silence_ms> ms of silence
  And the silence threshold is 500 ms
  When push-to-talk is released
  Then the text delivered to the focused field is "<spoken>"
  And the trimmed audio sent to the model contains no more than 500 ms of trailing silence

  Examples:
    | spoken          | silence_ms |
    | hello world     | 800        |
    | open the door   | 1500       |
```

**(c) Filler-word removal**

```gherkin
# ❌ BAD — couples to implementation (regex), vague oracle
Scenario: Remove fillers
  Given the filler regex is applied to the transcript
  When processing happens
  Then it looks cleaner
```

```gherkin
# ✅ GOOD — behavior + concrete examples
@WHISPER-122
Scenario Outline: Filler words are removed from the delivered text
  Given the model will transcribe the audio to "<raw>"
  And filler-word removal is enabled
  When push-to-talk is released
  Then the text delivered to the focused field is "<clean>"

  Examples:
    | raw                              | clean                  |
    | um so basically send the report  | send the report        |
    | I you know need coffee           | I need coffee          |
```

**(d) Model CPU fallback when Vulkan is absent**

```gherkin
# ❌ BAD — leaks runtime/driver internals, asserts on logs
Scenario: GPU stuff
  Given vulkan-1.dll is not present in System32
  When WhisperFactory.CreateBuilder is called with GpuLayerCount=20
  Then an exception is caught and logged at Warning level

# ✅ GOOD — describes the user-observable policy
@WHISPER-130
Scenario: Transcription falls back to CPU when no compatible GPU runtime is present
  Given no compatible GPU runtime is available
  When a transcription is requested
  Then the transcription completes using the CPU backend
  And the user is not shown an error
```

---

## 5. Fully worked example, end to end

Feature: **"transcription is delivered to the focused field on push-to-talk release."** Below: the `.feature`, the Application command + handler it drives, and the binding/step + driver.

### 5.1 The Application command and handler (production code it drives)

```csharp
// Application/Transcription/DeliverTranscriptionCommand.cs
// CQRS command using the custom Mediator marker (NOT MediatR).
public sealed record DeliverTranscriptionCommand(AudioClip Clip) : ICommand<DeliveryResult>;

public sealed record DeliveryResult(bool Delivered, string Text);
```

```csharp
// Application/Transcription/DeliverTranscriptionHandler.cs
// Orchestration only: trim silence (Logic), transcribe (port), clean (Logic), inject (port).
public sealed class DeliverTranscriptionHandler
    : ICommandHandler<DeliverTranscriptionCommand, DeliveryResult>
{
    private readonly ISilenceTrimmer _silenceTrimmer;     // Logic.AudioManagement
    private readonly ITranscriber _transcriber;            // Infrastructure port (Whisper.net)
    private readonly IFillerWordCleaner _cleaner;          // Logic.AudioManagement / TextProcessing
    private readonly ITextInjector _injector;              // Infrastructure port (SendInput)

    public DeliverTranscriptionHandler(
        ISilenceTrimmer silenceTrimmer,
        ITranscriber transcriber,
        IFillerWordCleaner cleaner,
        ITextInjector injector)
    {
        _silenceTrimmer = silenceTrimmer;
        _transcriber = transcriber;
        _cleaner = cleaner;
        _injector = injector;
    }

    public async ValueTask<DeliveryResult> Handle(
        DeliverTranscriptionCommand command, CancellationToken ct)
    {
        var trimmed = _silenceTrimmer.Trim(command.Clip);          // pure Logic
        var raw = await _transcriber.TranscribeAsync(trimmed, ct); // faked in specs
        var clean = _cleaner.Clean(raw.Text);                       // pure Logic

        if (string.IsNullOrWhiteSpace(clean))
            return new DeliveryResult(Delivered: false, Text: string.Empty);

        _injector.Inject(clean);                                    // faked in specs
        return new DeliveryResult(Delivered: true, Text: clean);
    }
}
```

### 5.2 The feature file

```gherkin
# Features/PushToTalk/DeliverOnRelease.feature
Feature: Deliver transcription on push-to-talk release
  As someone dictating into any application
  I want the recognized text inserted into the field I'm typing in
  So that I can speak instead of type

  Background:
    Given filler-word removal is enabled
    And the silence threshold is 500 ms

  @WHISPER-101
  Scenario: Spoken phrase is delivered to the focused field
    Given the model will transcribe the audio to "schedule the meeting for friday"
    When push-to-talk is released
    Then the text delivered to the focused field is "schedule the meeting for friday"

  @WHISPER-101
  Scenario: Nothing is delivered when the audio yields no speech
    Given the model will transcribe the audio to ""
    When push-to-talk is released
    Then no text is delivered to the focused field
```

### 5.3 Support: scenario world + hooks

```csharp
// Support/ScenarioWorld.cs — scenario-scoped captured state (resolved via DI scope).
public sealed class ScenarioWorld
{
    public AudioClip CapturedClip { get; set; } = AudioClip.OneSecondOfSilence();
    public DeliveryResult? LastResult { get; set; }
}
```

```csharp
// Support/Hooks.cs
[Binding]
public sealed class Hooks
{
    private readonly IFillerWordCleaner _cleaner; // example of toggling real config per scenario
    public Hooks(/* inject what you need */) { }

    [BeforeScenario]
    public void ResetState() { /* DI gives a fresh scope per scenario; usually nothing to do */ }
}
```

### 5.4 Driver

```csharp
// Drivers/TranscriptionDriver.cs
public sealed class TranscriptionDriver
{
    private readonly IMediator _mediator;
    private readonly ITranscriber _transcriber;
    private readonly ITextInjector _injector;
    private readonly ScenarioWorld _world;

    public TranscriptionDriver(
        IMediator mediator, ITranscriber transcriber,
        ITextInjector injector, ScenarioWorld world)
    {
        _mediator = mediator;
        _transcriber = transcriber;
        _injector = injector;
        _world = world;
    }

    public void ModelWillTranscribeTo(string text) =>
        _transcriber
            .TranscribeAsync(Arg.Any<AudioClip>(), Arg.Any<CancellationToken>())
            .Returns(new TranscriptionResult(text));

    public async Task ReleasePushToTalk() =>
        _world.LastResult = await _mediator.Send(new DeliverTranscriptionCommand(_world.CapturedClip));

    public void AssertDelivered(string expected) =>
        _injector.Received(1).Inject(expected);

    public void AssertNothingDelivered() =>
        _injector.DidNotReceive().Inject(Arg.Any<string>());
}
```

### 5.5 Step definitions (thin)

```csharp
// StepDefinitions/PushToTalkSteps.cs
[Binding]
public sealed class PushToTalkSteps
{
    private readonly TranscriptionDriver _driver;
    public PushToTalkSteps(TranscriptionDriver driver) => _driver = driver;

    [Given(@"the model will transcribe the audio to ""(.*)""")]
    public void GivenModelTranscribesTo(string text) => _driver.ModelWillTranscribeTo(text);

    [When(@"push-to-talk is released")]
    public Task WhenReleased() => _driver.ReleasePushToTalk();

    [Then(@"the text delivered to the focused field is ""(.*)""")]
    public void ThenDelivered(string expected) => _driver.AssertDelivered(expected);

    [Then(@"no text is delivered to the focused field")]
    public void ThenNothingDelivered() => _driver.AssertNothingDelivered();
}
```

(The `Background` steps — filler removal on, 500 ms threshold — live in a small `ConfigSteps` binding that flips the real config the handler reads.)

**What just happened, in the double-loop terms of section 1:** you wrote 5.2 first (red — no handler). To make it green you TDD'd 5.1 with xUnit (`DeliverTranscriptionHandlerTests`, `SilenceTrimmerTests`, `FillerWordCleanerTests`), each red→green→refactor. Once those units exist, the scenario goes green. AwesomeAssertions does the asserting in the xUnit tests; NSubstitute's `Received`/`DidNotReceive` do it at the behavior boundary in the Driver.

---

## 6. What to BDD vs what NOT to

### Decision table

| Layer / concern | Gherkin (Reqnroll)? | Plain xUnit? | Manual / iterative? | Why |
|---|---|---|---|---|
| **Domain rules** (invariants, value objects' behavior in context) | ✅ key business rules | ✅ exhaustive edge cases | — | Rules are behavior; their boundary cases are not worth Gherkin. |
| **Application orchestration** (handlers wiring Logic + ports) | ✅ this is the sweet spot | ✅ error paths, cancellation | — | Orchestration *is* observable behavior driven via `IMediator`. |
| **`Logic.*` behaviors** (silence trim, filler removal, CPU-fallback policy) | ✅ the policy/intent | ✅ numeric/branch detail | — | Express the *policy* in Gherkin; pound the *math* in xUnit. |
| **Pure functions / value objects / parsers** | ❌ | ✅ | — | No conversation value; Gherkin would just be a noisy unit test. |
| **Mapperly mappings** | ❌ | ✅ (a couple of round-trips) | — | Mechanical; assert mapping correctness in xUnit. |
| **Infrastructure adapters** (Whisper.net, NAudio, SendInput, Ollama, persistence) | ❌ (faked in specs) | ⚠️ thin integration tests, `@slow` | ✅ real-device smoke | External I/O; verify the *contract* via the faked port in specs, the *adapter* via a few integration tests. |
| **WPF visual / overlay rendering / tray UX** | ❌ **never through Gherkin** | ❌ | ✅ manual, iterative | UI is intentionally iterative; driving pixels through Gherkin is the classic anti-pattern. |
| **ViewModel logic** (CommunityToolkit.Mvvm commands/state) | ⚠️ only if it encodes real behavior | ✅ for non-trivial VM logic | ✅ for layout/binding | Test logic, not chrome. |

### Tie to the philosophy: no meaningless tests, ~80% guideline

- **Coverage is a smoke detector, not a goal.** ~80% is where this kind of codebase usually lands when you've genuinely covered behavior + edge cases. Chasing the last 20% (WPF code-behind, trivial DTOs, generated mapper internals) produces tests that assert nothing meaningful — **don't write them.** Configure coverlet to **exclude** Presentation and generated code so the number reflects testable behavior.
- **Prefer no test over a box-checking test.** A scenario that re-states the handler line-by-line, or a unit test that mocks everything and asserts the mock was called, adds maintenance cost and zero confidence. If you can't name the behavior it protects, delete it.

### The classic BDD failure modes (and the guardrail for each)

1. **Imperative step explosion** — hundreds of `When I click...` steps, no reuse. **Guard:** declarative phrasing (section 4) + Drivers (section 3); review any step longer than 3 lines.
2. **Testing the GUI through Gherkin** — slow, flaky, couples specs to layout. **Guard:** specs reference *only* `IMediator` and faked ports; `Specs` does not reference `Presentation` (enforced by project references).
3. **Shared mutable scenario state** — order-dependent, flaky scenarios. **Guard:** per-scenario DI scope from the plugin; carry state in a scenario-scoped `ScenarioWorld`, never in `static` fields.
4. **Scenarios that mirror code instead of behavior** — "the handler calls `_cleaner.Clean`." **Guard:** if a non-programmer can't read the scenario as a sentence about the product, rewrite it; assert on outcomes, not on internal calls (the one acceptable "interaction" assertion is at the *port boundary*, e.g. "text was delivered").

---

## 7. Definition of Done + traceability

### How "validate AC and behavioral descriptions" becomes concrete

The team's DoD says: *validation of acceptance criteria AND behavioral descriptions.* Operationalize it as:

> A requirement is **Done** when every acceptance criterion has a corresponding **green Reqnroll scenario** tagged with the requirement's Plane issue id, the supporting units are covered by green xUnit tests, and the living-documentation HTML report renders the scenario as passing.

That is checkable in CI, not a matter of opinion.

### Tag every scenario with its Plane issue id

```gherkin
@WHISPER-114
Scenario Outline: Trailing silence beyond the threshold is trimmed before delivery
```

This gives **bidirectional traceability**: from a Plane issue you can grep `@WHISPER-114` to find its executable spec; from a failing scenario the report names the issue it implements. A small CI step (or a `dotnet test --filter "Category=WHISPER-114"`-style run, since Reqnroll maps tags to xUnit traits) can prove an issue's scenarios pass before it's marked Done.

### Living documentation as the behavioral record

The native HTML report (`formatters.html` in `reqnroll.json`, section 2) regenerates every run from Cucumber Messages. ([Reqnroll roadmap: HTML report](https://reqnroll.net/news/2025/06/roadmap-update-html-report/)) Publish it as a CI artifact. It *is* the "behavioral description" deliverable in the DoD — a human-readable, always-current catalog of what the system does, with pass/fail status. No separate "behavioral docs" to keep in sync.

### How coverage and BDD complement each other

- **Scenarios prove the right behaviors exist and pass** (breadth of intent).
- **Coverage proves you didn't leave whole branches untested** (depth of execution). A green scenario suite with 40% coverage means lots of code runs paths no test asserts — investigate. 80%+ with the Presentation/generated exclusions, plus a passing tagged-scenario set, is the healthy signal.
- Run coverlet over **both** the unit and spec projects; ReportGenerator merges them. The scenarios' execution counts toward coverage, which is fine — but never *write* a scenario to bump coverage. Coverage is a downstream measurement of behavior-driven work, never its driver.

---

## 8. Adoption path for a BDD beginner

### Ordered onboarding

1. **Module 0 — prove the harness with ONE feature.** Before any real feature, wire the whole pipeline end-to-end for the push-to-talk delivery example in section 5: project, `reqnroll.json`, DI plugin against the real `AddApplication()`, one Driver, one green scenario, the HTML report generating, CI running it. Don't expand until this loop is green and fast. This is where you learn the tooling without also fighting domain complexity.
2. **Adopt the double loop on the next real requirement.** Write the failing scenario first; drop into xUnit TDD to build the handler/Logic; close the loop. Resist writing code before the scenario.
3. **Build the Driver library as you go.** Each new capability adds a Driver; step definitions stay one-liners. After 3–4 features you'll have a reusable vocabulary of steps.
4. **Introduce Scenario Outlines** once you hit your first "same behavior, many inputs" case (VAD thresholds, filler lists) — don't force them early.
5. **Tag for traceability from scenario #1** (`@WHISPER-xxx`); retrofitting tags is tedious.
6. **Review scenarios as specs, not code,** in PRs. A reviewer should be able to read the `.feature` and say "yes, that's the behavior we agreed on" without reading the bindings.

### Pitfalls to watch (beginner edition)

- Writing Gherkin **after** the code → you've built a worse unit-test DSL. Write it first, or at least before the handler exists.
- Putting logic in step definitions → move it to a Driver immediately; it never gets better on its own.
- One giant `CommonSteps` class with regex soup → group steps by capability, mirror your Features folders.
- Faking *too deep* (faking Logic) → fake **only Infrastructure ports**; let real Logic + handlers run, or the scenario tests nothing.
- `static` scenario state → use the scenario-scoped DI container + `ScenarioWorld`.
- Trying to BDD the WPF UI → don't; keep UI iterative and manual.

### Checklist: "definition of a good scenario"

- [ ] Reads as a sentence a non-programmer understands.
- [ ] Describes **one** behavior (no "and" in the title).
- [ ] **Declarative** — no widgets, key codes, or API names.
- [ ] Uses **ubiquitous language** matching Domain/Application types.
- [ ] Contains **only** data that affects the outcome.
- [ ] Asserts on **observable behavior** (result or port boundary), not internal calls.
- [ ] Tagged with its **Plane issue id**.
- [ ] Step definitions are **thin** (delegate to a Driver).
- [ ] Independent of other scenarios (no shared mutable state).
- [ ] You can name the **requirement it protects** — if not, it shouldn't exist.

---

## Sources

- SpecFlow EOL + Reqnroll as maintained successor: <https://reqnroll.net/news/2025/01/specflow-end-of-life-has-been-announced/>
- Reqnroll overview / context: <https://seankilleen.com/2025/01/farewell-specflow-gaspar-nagy-saves-the-day-with-reqnroll/>
- `Reqnroll.xunit.v3` (v3.3.4, .NET 10 compatible): <https://www.nuget.org/packages/Reqnroll.xunit.v3/>
- xUnit v3 support / Reqnroll v3.1 release: <https://reqnroll.net/news/2025/09/reqnroll-v3-1-released/>
- `Reqnroll.Microsoft.Extensions.DependencyInjection` (v3.3.4): <https://www.nuget.org/packages/Reqnroll.Microsoft.Extensions.DependencyInjection>
- DI plugin `[ScenarioDependencies]` usage / per-scenario scoping: <https://docs.reqnroll.net/latest/integrations/dependency-injection.html>
- Native HTML report (V3, `formatters` in `reqnroll.json`, free/OSS): <https://reqnroll.net/news/2025/06/roadmap-update-html-report/>
- Living documentation status / alternatives discussion: <https://github.com/orgs/reqnroll/discussions/196>
- .NET 10 LTS GA (2025-11-11, supported to 2028): <https://devblogs.microsoft.com/dotnet/announcing-dotnet-10/>

---

## Module 0 — BDD enablement checklist

Concrete setup tasks that belong in the project-setup module (do these before any feature work):

- **Projects**
  - [ ] Create `tests/Dictation.Specs/` acceptance project, `net10.0`, `<OutputType>Exe</OutputType>` (xUnit v3 requirement).
  - [ ] Reference `Domain`, `Application`, all `Logic.*` — **not** `Presentation`.
  - [ ] Create the unit-test projects (`Domain.Tests`, `Application.Tests`, `Logic.*.Tests`) separately.
- **Packages** (pin to current: 3.3.4 line for Reqnroll)
  - [ ] `Reqnroll.xunit.v3` `3.3.4`
  - [ ] `Reqnroll.Microsoft.Extensions.DependencyInjection` `3.3.4`
  - [ ] `xunit.v3`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`
  - [ ] `NSubstitute`, `AwesomeAssertions`, `coverlet.collector`
- **Config**
  - [ ] Add `reqnroll.json` with `formatters.html` → `TestResults/living-doc.html`.
  - [ ] Add `Support/TestDependencies.cs` with `[ScenarioDependencies]` calling the **real** `AddApplication()` / `AddLogicServices()` and registering NSubstitute fakes for Infrastructure ports only.
  - [ ] Add `Support/ScenarioWorld.cs` (scenario-scoped) and `Support/Hooks.cs`.
- **Folder skeleton**
  - [ ] `Features/`, `StepDefinitions/`, `Drivers/`, `Support/`, `Fixtures/wav/`.
- **Sample feature (proves the harness)**
  - [ ] `Features/PushToTalk/DeliverOnRelease.feature` + `PushToTalkSteps` + `TranscriptionDriver` (section 5), driving a real `DeliverTranscriptionCommand` handler.
  - [ ] One WAV fixture in `Fixtures/wav/`.
  - [ ] Scenario tagged `@WHISPER-<id>` and green locally.
- **CI wiring**
  - [ ] `dotnet test` over the solution (units + specs) on each PR.
  - [ ] Coverlet collection; ReportGenerator merge; **exclude** `Presentation` + generated code.
  - [ ] Publish `living-doc.html` and the coverage report as build artifacts.
  - [ ] Optional gate: fail PR if any non-`@wip` scenario fails; coverage is **reported, not gated**.
- **Docs**
  - [ ] Commit this `docs/bdd-strategy.md`.
  - [ ] Add a one-paragraph "How we test" pointer in the repo README linking here and stating the DoD rule (green tagged scenario per AC).
