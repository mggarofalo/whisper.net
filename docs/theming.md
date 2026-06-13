# Theming decision

This records the theming decision for the app's Light/Dark and accent colour support.

## Decision

**Use WPF's built-in Fluent theme via `Application.ThemeMode = ThemeMode.System`.** The app follows the OS
**Light/Dark** preference and the system **accent** colour, applied app-wide from `App.OnStartup`. No
third-party UI dependency is added.

## Options considered

| Option | Pros | Cons |
| --- | --- | --- |
| **Built-in WPF Fluent (`ThemeMode`)** — chosen | No dependency; ships with .NET; honours system Light/Dark + accent + Mica; one-line, isolated opt-in | Experimental in .NET 10 (`WPF0001`); not a full Fluent 2 control set (no `NavigationView`); some styling gaps |
| Library (WPF-UI / iNKORE.UI.WPF.Modern) | Real Fluent 2 `NavigationView` and a complete control set | Adds a dependency to maintain and keep in lockstep with .NET; larger surface that can destabilize working logic |

## Rationale

The guiding constraint is "keep isolated so it
cannot destabilize working logic." The built-in theme meets the acceptance criteria (system Light/Dark +
accent, themed settings window) with the **smallest possible, fully isolated change** — a single
`ThemeMode = ThemeMode.System` opt-in guarded against the `WPF0001` experimental diagnostic. Adopting a
library would buy a richer control set we do not currently need (the settings shell is a simple tabbed view,
not a `NavigationView`) at the cost of a new dependency and a much larger blast radius. If a future milestone
wants a full Fluent 2 navigation experience, revisit this and adopt WPF-UI then.

## Isolation & risk

The opt-in is one line in `App.OnStartup`, wrapped in `#pragma warning disable/restore WPF0001`. It changes
only the visual theme; all behavior lives in the WPF-free view-models and is unaffected. The full non-`@wip`
suite (validation, instant-apply, the hotkey/device/model pickers, accessibility) passes unchanged under the
theme, so the active theme is honoured throughout.
