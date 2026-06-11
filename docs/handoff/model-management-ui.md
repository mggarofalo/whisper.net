# Handoff: Model-management UI cluster — WHISPER-104 → 107 → 105

Work these **in this order**. 104 is trivial and isolated. 107 is the structural change (per-row
download state); 105's layout work sits on top of 107's row view-models — doing 105 first would be
rework. All three touch `src/Presentation/Shell/Views/ModelView.xaml` and
`src/Logic.AppManagement/Shell/ModelViewModel.cs`, so strictly sequential.

Fetch full descriptions/ACs from Plane (UUIDs in `README.md`). Queue-level notes from the original
briefing plus what this session learned:

## WHISPER-104 — Model list pane has no scrollbar (trivial)

- Pure XAML: the model list pane doesn't scroll when content exceeds the viewport. Expect a
  `ScrollViewer` wrap or `ScrollViewer.VerticalScrollBarVisibility="Auto"` on the items control in
  `ModelView.xaml`.
- Views are smoke-test-only — there is no meaningful Reqnroll scenario for a scrollbar. The
  dod-validator accepts skills-only/manual validation **when the PR records the exception**: state
  in the PR body that this is a pure-XAML change validated by the smoke harness + manual check, and
  put the manual check ("resize window small, model list scrolls") in the Plane close-out comment.
- Branch: `fix/whisper-104-model-pane-scrollbar`. PR title subject lowercase.
- Note: 108 (merged) added a loading-state element to `ModelView.xaml` — pull fresh main first.

## WHISPER-107 — Concurrent model downloads with per-row state (structural)

- Today's model list presumably tracks one download at a time on the section VM (shared
  IsDownloading/progress on `ModelViewModel`). The issue wants **per-row VM state** so multiple
  models can download concurrently, each row showing its own progress/state.
- Expect: a `ModelRowViewModel` (or similar) in `Logic.AppManagement` owning per-model
  Download/Cancel commands + progress; `ModelViewModel` becomes a collection coordinator.
  Check how downloads flow today: Application command/handler (likely a `DownloadModelCommand` and
  an `IModelDownloader`/`IModelStore` port implemented in Infrastructure) and whether the handler
  supports concurrent invocations. `Logic.ModelManagement` holds the model-catalog logic
  (`tests/Logic.ModelManagement.Tests` exists — 23 tests).
- Watch for: progress reporting marshaling (background thread → VM observable — the sanctioned
  pattern from WHISPER-91 is the background-thread collection-update discipline; see commit
  0357682), cancellation per row, double-click double-download dedupe (per-row guard), and what
  happens to a row mid-download when the section deactivates (cached VMs — WHISPER-89 — keep
  living; downloads should continue and the row state should still be correct on return).
- Specs: `@WHISPER-107` scenarios driving the real ModelViewModel through Mediator with the
  downloader port substituted (controllable per-model completion so two in-flight downloads can be
  asserted independently). The active-model persistence work from WHISPER-98 (`fix/whisper-98-...`)
  is nearby — don't regress its scenarios.
- Branch: `feat/whisper-107-concurrent-model-downloads`.

## WHISPER-105 — Compact the model table layout (on top of 107)

- Layout/density pass on the model table: smaller rows, tighter columns. Build it on 107's row VMs
  (the row template will have just been restructured — that's why 105 goes after).
- Mostly XAML; same dod-validator note as 104 (record the spec exception in the PR; manual check in
  the close-out comment). Any behavior moved/added during the compaction belongs in
  `Logic.AppManagement` with unit/spec coverage.
- Keep the WHISPER-96 smoke/template-completeness gate green — template changes are exactly what it
  exists to catch.
- Branch: `chore/whisper-105-compact-model-table` (or `feat/` if it adds visible capability).
