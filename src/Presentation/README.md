# Presentation

The WPF tray application and **composition root**. The only project permitted to reference
Infrastructure; it wires every layer together via the Generic Host (WHISPER-57). The tray icon,
level overlay, and settings UI arrive in later modules (M6/M10).

**Depends on:** everything (Application, Domain, all `Logic.*`, Infrastructure). Windows-only (WPF).
