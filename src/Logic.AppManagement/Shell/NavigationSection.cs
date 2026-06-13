// One registered navigation destination in the dashboard shell: a stable key the shell
// navigates by (and labels its nav region with) plus the view-model type the navigation service
// resolves from the DI container when that section becomes active. Registering sections as DI services
// — rather than hard-coding a switch — is what lets the shell stay open to new feature views (the
// model picker, settings, history, stats) as later M10 issues add them.

namespace Logic.AppManagement.Shell;

public sealed record NavigationSection(string Key, Type ViewModelType);
