// Turns WPF data-binding trace errors into test failures (WHISPER-96 AC1). WPF reports a broken
// binding path as a trace-level message and otherwise carries on silently — exactly the failure mode
// the smoke layer exists to catch. While in scope, every error-level entry on the data-binding trace
// source is collected; tests assert the collection is empty after the first bind.

using System.Diagnostics;

namespace Presentation.Smoke.Tests;

internal sealed class BindingErrorCollector : TraceListener
{
	private readonly SourceLevels _previousLevel;

	public List<string> Errors { get; } = [];

	public BindingErrorCollector()
	{
		PresentationTraceSources.Refresh();
		_previousLevel = PresentationTraceSources.DataBindingSource.Switch.Level;
		PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
		PresentationTraceSources.DataBindingSource.Listeners.Add(this);
	}

	public override void Write(string? message)
	{
	}

	public override void WriteLine(string? message)
	{
		if (message is not null)
		{
			Errors.Add(message);
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			PresentationTraceSources.DataBindingSource.Listeners.Remove(this);
			PresentationTraceSources.DataBindingSource.Switch.Level = _previousLevel;
		}

		base.Dispose(disposing);
	}
}
