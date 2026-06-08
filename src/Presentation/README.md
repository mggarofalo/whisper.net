# Presentation

The WPF tray application and **composition root**. The only project permitted to reference
Infrastructure; it wires every layer together via the Generic Host (WHISPER-57). The tray icon and
level overlay arrive in M6/M7; the dashboard & settings UI is built iteratively in M10.

The view-models are **not** here — they are WPF-free and live in `Logic.AppManagement` (the dashboard
`ShellViewModel` and its feature view-models, the `TrayController`, the `LevelOverlayController`), so
their behavior is driven for real in the Reqnroll specs. This project holds only the thin views that
bind to them (the tray icon, the level overlay, the dashboard `ShellWindow` and its section views),
verified by manual smoke. The dashboard shell resolves each section's view-model from the DI container
via the `INavigationService` (WHISPER-19).

**Depends on:** everything (Application, Domain, all `Logic.*`, Infrastructure). Windows-only (WPF).
