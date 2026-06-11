# Handoff: WHISPER-100…112 backlog (filed 2026-06-10)

Session ended 2026-06-11 after completing 5 of 13 issues. This directory hands off the remaining 8.
Companion docs (read the one for the issue you're picking up):

- [`model-management-ui.md`](model-management-ui.md) — WHISPER-104 → 107 → 105 (work in that order)
- [`settings-shell.md`](settings-shell.md) — WHISPER-103, 106
- [`overlay.md`](overlay.md) — WHISPER-101 → 100 → 102 (102 strictly last)

## State of the world

| Issue | Title | PR | Status |
|---|---|---|---|
| WHISPER-109 | Hotkey assignment broken / Current "(none)" | [#95](https://github.com/mggarofalo/whisper.net/pull/95) | Merged, Done |
| WHISPER-110 | History/Stats empty | [#96](https://github.com/mggarofalo/whisper.net/pull/96) | Merged, Done |
| WHISPER-112 | Final word clipped on release | [#97](https://github.com/mggarofalo/whisper.net/pull/97) | Merged, Done |
| WHISPER-111 | 30 s cap silently drops audio | [#98](https://github.com/mggarofalo/whisper.net/pull/98) | Merged, Done |
| WHISPER-108 | Auto-load sections on first activation | [#99](https://github.com/mggarofalo/whisper.net/pull/99) | Merged, Done |
| WHISPER-104 | Model list pane has no scrollbar | — | Backlog, not started |
| WHISPER-107 | Concurrent model downloads, per-row state | — | Backlog, not started |
| WHISPER-105 | Compact model table layout | — | Backlog, not started |
| WHISPER-103 | Sidebar dark-theme restyle (WCAG AA) | — | Backlog, not started |
| WHISPER-106 | Home tab: purpose or removal | — | Backlog, not started |
| WHISPER-101 | Overlay meter dB mapping | — | Backlog, not started |
| WHISPER-100 | Overlay bottom-center placement | — | Backlog, not started |
| WHISPER-102 | Overlay state/elapsed/near-cap feedback | — | Backlog, not started |

Also filed this session: **WHISPER-113** — `AppSettings.SilenceThresholdMs` is wired to nothing
(found during 112's diagnostic; connect it to trimming/VAD or remove it).

**Main is at `4a35ecd`** (post-#99). **Fast-gate baseline: 843 tests, all green**
(`dotnet test Whisper.slnx --filter "Category!=wip&Category!=slow"`).

## Plane bookkeeping (CLI quirks included)

Workspace `dev`, project `WHISPER`. `issue get`/`update` take `--resource-id <UUID>` (sequence ids
do NOT resolve); `link add` takes `--work-item-id WHISPER-<n>`. States resolve by name.

| Issue | UUID |
|---|---|
| WHISPER-100 | `eae86bb1-0d6b-439b-bbbe-ff3547f60ad3` |
| WHISPER-101 | `84facde5-e8a6-4dc5-a828-14082cb0b991` |
| WHISPER-102 | `ee995270-e32a-46bf-9cb9-1f62dab3911e` |
| WHISPER-103 | `8a280f21-d5ff-4eef-a85f-85a22a6fcfb3` |
| WHISPER-104 | `42fecaac-0f08-4e62-8d92-b33a2c4fc946` |
| WHISPER-105 | `90cbdc1d-cd41-4814-9e35-ea02974d8254` |
| WHISPER-106 | `c1cfd395-d19c-4a49-8ac5-08aad64ed82d` |
| WHISPER-107 | `a24e0934-2372-45b2-a9e0-297419c2db3d` |

Fetch the full description/AC per issue with:
`plane issue get --resource-id <uuid> -w dev -p WHISPER -o json` (read `description_html`).

## Per-issue playbook (what this session actually did, repeat it)

1. `git checkout main && git pull` — always start from fresh main; the issues share files, strictly one at a time.
2. Move the issue to **In Progress**; create a worktree:
   `git worktree add .claude/worktrees/<branch> -b <type>/whisper-<id>-<slug>`.
3. **Spec-first**: failing `@WHISPER-<id>` Reqnroll scenarios for each behavioral AC, then implement
   to green. Confirm red before fixing — it's the evidence the scenario tests the right thing.
4. Local gates before any PR (this is the real safety net — auto-merge fires on the title check, NOT the build):
   - `dotnet build Whisper.slnx -p:TreatWarningsAsErrors=true`
   - `dotnet test Whisper.slnx --filter "Category!=wip&Category!=slow"` (843 baseline)
   - `dotnet format Whisper.slnx --verify-no-changes` (new files usually need one `dotnet format` pass for line endings)
5. PR: conventional title, **subject starts lowercase** (CI gate rejects otherwise). Document root
   cause / design decisions in the body. `plane link add` the PR to the issue. `gh pr merge <n> --squash --auto`.
6. Adversarial bug analysis (`/find-bugs` or equivalent reviewer pass) on the diff; fix all findings.
7. **dod-validator gate before Done** (`.claude/skills/dod-validator/SKILL.md`): every AC → green
   tagged scenario (`dotnet test --filter "Category=WHISPER-<id>"`), full suite green, build/format
   clean, no mock-restating ("asserted-but-not-validated" scenarios don't count).
8. After squash-merge: pull main, `git worktree remove <path> --force` (on Windows this sometimes
   hits a file lock from testhost — `git worktree prune` + delete the directory manually), delete the
   local branch, post a close-out comment on the issue, move it to **Done**.
9. Anything not verifiable autonomously (real mic, global hotkeys, multi-monitor): implement, cover
   the seams with specs/units, list it as a manual-verification checklist in the Plane close-out
   comment, and keep moving.
10. If an issue's stated hypothesis proves wrong in the code, post the corrected diagnosis as a
    Plane comment **before** implementing (this happened on 109 and 110 — see their comment threads).

## Repo gotchas learned this session

- **Dictation.Specs is net10.0 and cannot reference WPF.** Testable logic lives in
  `Logic.AppManagement`; views are smoke-test-only (WHISPER-96 harness, `Presentation.Smoke.Tests` —
  it includes a template-completeness gate, so new XAML resources/templates may need smoke updates).
- **Specs share one root DI provider**; mutable substitutes are scoped per scenario in
  `tests/Dictation.Specs/Support/TestDependencies.cs`. Follow the existing scoped-override patterns
  (`ScenarioAudioBufferingOptions` is the model for overriding an options record per scenario).
- Drivers enter VMs through the **real lifecycle** (`OnNavigatedTo()`, or
  `ShellViewModel.NavigateCommand` → `NavigationService`), never `LoadCommand` directly —
  `SectionAutoLoadDriver` (108) and `HotkeyAssignmentDriver` (109) are the references.
- `ManualTimeProvider` exists in `tests/Dictation.Specs/Support/` and
  `tests/Logic.AppManagement.Tests/Support/`. Specs override `TimeProvider.System` scoped.
- The `@WHISPER-60` guidance spec fails locally unless Git's coreutils are on PATH:
  `$env:PATH += ";C:\Program Files\Git\usr\bin"` before `dotnet test`.
- Long test/build commands: give them 10-minute timeouts; first build of a worktree is slow.
- QA habit that paid off every time: **mutation-test the new pins** (flip a comparison, drop a
  guard, re-run) — three real escapes were found this way across 110/111/112.

## What WHISPER-102 inherits from this session (do not lose this)

WHISPER-111 introduced the messages 102 must surface (all in `src/Application/Dictation/`,
published via `IMessenger` from `DictationOrchestrator`, duration-only payloads `(RecordedMs, LimitMs)`):

- `DictationNearLimitMessage` — 80% of the soft limit (default soft limit 10 min)
- `DictationAtLimitMessage` — soft limit reached (recording continues)
- `DictationHardLimitStopMessage` — hard failsafe (default 20 min) auto-stopped and transcribed

They currently have **zero consumers** — 102 binds them to the overlay. See `overlay.md`.
