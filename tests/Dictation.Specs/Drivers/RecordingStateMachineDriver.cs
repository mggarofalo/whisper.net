// Drives the @WHISPER-22 recording-state-machine scenarios. It owns HOW the machine is exercised so
// the steps stay one-liners: it issues start/stop/complete/cancel against the REAL
// RecordingStateMachine and records the state path it travels plus whether a cancel discarded the
// capture. "No text is delivered" is asserted behaviorally — a cancelled capture never enters
// Transcribing, the only state from which delivery follows.

using AwesomeAssertions;
using Domain.Recording;
using Logic.AppManagement;

namespace Dictation.Specs.Drivers;

public sealed class RecordingStateMachineDriver
{
	private readonly RecordingStateMachine _machine;
	private readonly List<RecordingState> _visited = [];
	private bool _cancelled;

	public RecordingStateMachineDriver(RecordingStateMachine machine)
	{
		_machine = machine;
		_machine.StateChanged += (_, e) => _visited.Add(e.Current);
		_machine.Cancelled += (_, _) => _cancelled = true;
	}

	public void StartRequest() => _machine.RequestStart();

	public void StopRequest() => _machine.RequestStop();

	public void TranscriptionCompletes() => _machine.CompleteTranscription();

	public void PressEsc() => _machine.Cancel();

	// --- assertions ---

	public void AssertState(string state) =>
		_machine.State.Should().Be(Enum.Parse<RecordingState>(state, ignoreCase: true));

	public void AssertCaptureDiscarded() => _cancelled.Should().BeTrue();

	// Delivery only ever follows the Transcribing state; a cancelled capture must never have reached it.
	public void AssertNoTextDelivered() => _visited.Should().NotContain(RecordingState.Transcribing);
}
