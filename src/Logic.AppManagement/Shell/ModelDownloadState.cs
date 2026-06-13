// The terminal-aware state of a model row's download in the picker: not started, in
// progress (the percent is live), or finished one way or the other. The view binds to it to show a
// progress bar, a tick, or an error, and the picker uses it to decide whether a just-selected model is
// ready to activate.

namespace Logic.AppManagement.Shell;

public enum ModelDownloadState
{
	NotStarted,
	InProgress,
	Succeeded,
	Failed,
}
