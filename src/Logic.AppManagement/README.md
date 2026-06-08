# Logic.AppManagement

Application-lifecycle behaviors — profile activation, app-rule application, command mode, and
continuous-dictation policy. Pure logic; no OS or Infrastructure concerns.

Also home to the **WPF-free MVVM view-models** the thin WPF views bind to: the dashboard shell and its
navigation (`Shell/`), the tray controller, and the level-overlay controller. Built on
`CommunityToolkit.Mvvm` (UI-framework-agnostic) and depending only on `IMediator`, so their behavior is
driven for real in the Reqnroll specs.

**Depends on:** Application, Domain.
