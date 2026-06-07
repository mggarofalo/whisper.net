// Unit depth for the WHISPER-22 recording state machine, beyond the @WHISPER-22 acceptance scenarios.
// Pins down the legal path, the Esc cancel from either in-flight state, the no-op handling of every
// illegal transition (never an error state), and the observability of each accepted change.

using AwesomeAssertions;
using Domain.Recording;
using Logic.AppManagement;
using Xunit;

namespace Logic.AppManagement.Tests;

public sealed class RecordingStateMachineTests
{
	private readonly RecordingStateMachine _machine = new();
	private readonly List<RecordingStateChangedEventArgs> _changes = [];
	private int _cancellations;

	public RecordingStateMachineTests()
	{
		_machine.StateChanged += (_, e) => _changes.Add(e);
		_machine.Cancelled += (_, _) => _cancellations++;
	}

	[Fact]
	public void Starts_idle()
	{
		_machine.State.Should().Be(RecordingState.Idle);
	}

	[Fact]
	public void Walks_the_full_capture_cycle()
	{
		_machine.RequestStart();
		_machine.State.Should().Be(RecordingState.Recording);

		_machine.RequestStop();
		_machine.State.Should().Be(RecordingState.Transcribing);

		_machine.CompleteTranscription();
		_machine.State.Should().Be(RecordingState.Idle);

		_changes.Select(c => c.Current).Should().Equal(
			RecordingState.Recording, RecordingState.Transcribing, RecordingState.Idle);
	}

	[Fact]
	public void Esc_cancels_a_recording_back_to_idle_and_discards_the_capture()
	{
		_machine.RequestStart();

		_machine.Cancel();

		_machine.State.Should().Be(RecordingState.Idle);
		_cancellations.Should().Be(1);
		_changes.Select(c => c.Current).Should().NotContain(RecordingState.Transcribing);
	}

	[Fact]
	public void Esc_can_also_cancel_during_transcription()
	{
		_machine.RequestStart();
		_machine.RequestStop(); // now Transcribing

		_machine.Cancel();

		_machine.State.Should().Be(RecordingState.Idle);
		_cancellations.Should().Be(1);
	}

	[Fact]
	public void Esc_while_idle_is_a_no_op()
	{
		_machine.Cancel();

		_machine.State.Should().Be(RecordingState.Idle);
		_cancellations.Should().Be(0);
		_changes.Should().BeEmpty();
	}

	[Theory]
	[InlineData(RecordingState.Recording)]    // start while already recording
	[InlineData(RecordingState.Transcribing)] // start while transcribing
	public void A_start_request_is_ignored_unless_idle(RecordingState reached)
	{
		DriveTo(reached);
		int before = _changes.Count;

		_machine.RequestStart();

		_machine.State.Should().Be(reached);
		_changes.Count.Should().Be(before); // no transition occurred
	}

	[Fact]
	public void A_stop_request_is_ignored_unless_recording()
	{
		// Idle: ignored.
		_machine.RequestStop();
		_machine.State.Should().Be(RecordingState.Idle);

		// Transcribing: ignored.
		DriveTo(RecordingState.Transcribing);
		int before = _changes.Count;
		_machine.RequestStop();
		_machine.State.Should().Be(RecordingState.Transcribing);
		_changes.Count.Should().Be(before);
	}

	[Fact]
	public void Completing_transcription_is_ignored_unless_transcribing()
	{
		_machine.CompleteTranscription(); // from Idle
		_machine.State.Should().Be(RecordingState.Idle);

		_machine.RequestStart();
		_machine.CompleteTranscription(); // from Recording
		_machine.State.Should().Be(RecordingState.Recording);
	}

	private void DriveTo(RecordingState target)
	{
		if (target is RecordingState.Recording or RecordingState.Transcribing)
		{
			_machine.RequestStart();
		}

		if (target is RecordingState.Transcribing)
		{
			_machine.RequestStop();
		}
	}
}
