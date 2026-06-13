// The orchestrator's stop waits a post-release grace window so the device's in-flight
// capture tail drains into the clip. Under the scenario's manual clock that window never elapses on its
// own, so drivers that run a stop call this to drain it deterministically: begin the stop, elapse the
// grace window on the manual clock, then await the pipeline's completion. (Before the grace window this
// was just `await orchestrator.StopAsync()`.)

using Logic.AppManagement;
using Logic.AudioManagement;

namespace Dictation.Specs.Support;

public static class DictationStopExtensions
{
	public static async Task StopAndElapseGraceAsync(
		this DictationOrchestrator orchestrator,
		ManualTimeProvider time,
		AudioBufferingOptions bufferingOptions)
	{
		Task stop = orchestrator.StopAsync();
		time.Advance(TimeSpan.FromMilliseconds(bufferingOptions.PostReleaseGraceMs));
		await stop;
	}
}
