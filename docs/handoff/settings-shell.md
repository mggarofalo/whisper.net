# Handoff: Settings-shell cluster — WHISPER-103, WHISPER-106

Independent of the model-management cluster, but 106 touches the shell navigation that 103 restyles
— do 103 first if working both, and pull fresh main between them.

Fetch full descriptions/ACs from Plane (UUIDs in `README.md`).

## WHISPER-103 — Settings sidebar fails contrast — restyle to match the dark theme

- The sidebar (shell navigation list) doesn't meet contrast against the dark theme. The brief:
  restyle to **WCAG AA** — ≥ 4.5:1 for normal text, ≥ 3:1 for large text/UI components, in ALL
  states (default, hover, selected, selected+hover, focused, disabled).
- This is resource/style work in `src/Presentation` (look for theme resource dictionaries —
  brushes/colors — and the sidebar's ItemContainerStyle in the shell window XAML). Centralize
  colors in the theme dictionary rather than inline attributes so future theming stays one-place.
- Verification: compute contrast ratios for each state's fg/bg pair and put the numbers in the PR
  body (that's the evidence dod-validator/the reviewer can check; an `accessibility-audit` agent
  pass over the diff is a good extra gate here). No Reqnroll scenario is meaningful — record the
  spec exception in the PR, manual visual check in the close-out comment.
- Branch: `fix/whisper-103-sidebar-contrast`.

## WHISPER-106 — Home tab: give it a purpose or remove it

- Original briefing: *"build the status dashboard only if 110 is fixed and live data exists;
  otherwise remove the tab."* **That condition is now resolved: 110 is merged** — history entries
  and usage stats are real, live data (`BrowseHistory`, `GetUsageStats` through Mediator). So the
  dashboard option is on the table; removal remains the fallback if, on inspection, the dashboard
  would just duplicate the Stats section.
- Honest decision guidance from this session: Stats already shows totals and History shows entries
  (both auto-load on first activation since 108). A Home dashboard earns its keep only if it shows
  something the sections don't — e.g., at-a-glance current state (active model, current hotkey
  chord, last dictation, today's totals). If that reads as worth having, build it as a
  `HomeViewModel` in `Logic.AppManagement` composing existing queries (reuse `GetUsageStats`,
  `BrowseHistory` page 1, settings via `GetSettingsQuery`) with `@WHISPER-106` scenarios through
  the real Mediator; wire it into the 108 first-activation hook like the other sections. If not,
  delete the tab: remove the nav entry + view + VM registration, and update the WHISPER-96 smoke
  harness expectations.
- Either way the AC is "the tab has a purpose or doesn't exist" — don't leave a stub.
- Branch: `feat/whisper-106-home-dashboard` or `chore/whisper-106-remove-home-tab` depending on the
  call. If removing, say so plainly in the Plane close-out comment and PR body (the issue offers
  both outcomes, so neither needs re-approval).
