// Drives the real worker-supervision policy over a fake child-process seam. No native model or process
// is needed here: the scenarios govern restart/fallback/lifetime behavior, while focused Infrastructure
// tests cover the named-pipe and process adapters.

using Application.Ports;
using AwesomeAssertions;
using Domain.Audio;
using Domain.Models;
using Infrastructure.Transcription;
using Logic.GpuContactPoint;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class InferenceWorkerRecoveryDriver : IDisposable
{
	private readonly IGpuProbe _gpuProbe = Substitute.For<IGpuProbe>();
	private InferenceWorkerSupervisor? _supervisor;
	private ScenarioWorkerFactory? _factory;
	private TranscriptionResult? _result;
	private Exception? _failure;

	public void GivenCrashedGpuThenSuccessfulGpu() => Configure(
		true,
		WorkerOutcome.Sequence(
			new TranscriptionResult("warm"),
			new InferenceWorkerUnavailableException("worker unavailable")),
		WorkerOutcome.Success("recovered"));

	public void GivenGpuFailsTwiceThenCpuSucceeds() => Configure(
		true,
		WorkerOutcome.Fail,
		WorkerOutcome.Fail,
		WorkerOutcome.Success("cpu fallback"));

	public void GivenEveryRecoveryFails() => Configure(
		true,
		WorkerOutcome.Fail,
		WorkerOutcome.Fail,
		WorkerOutcome.Fail);

	public void GivenHealthyWarmedGpu() => Configure(
		true,
		WorkerOutcome.Success("warm", "first", "second"));

	public void GivenRunningWorker() => Configure(
		true,
		WorkerOutcome.Success("running"));

	public async Task WarmAndArmCrash()
	{
		EnsureConfigured();
		await _supervisor!.TranscribeAsync(Request(), CancellationToken.None);
	}

	public async Task RequestDictation()
	{
		EnsureConfigured();
		try
		{
			_result = await _supervisor!.TranscribeAsync(Request(), CancellationToken.None);
		}
		catch (Exception ex)
		{
			_failure = ex;
		}
	}

	public async Task RequestTwoDictations()
	{
		EnsureConfigured();
		await _supervisor!.TranscribeAsync(Request(), CancellationToken.None);
		TranscriptionResult first = await _supervisor.TranscribeAsync(Request(), CancellationToken.None);
		TranscriptionResult second = await _supervisor.TranscribeAsync(Request(), CancellationToken.None);
		_result = new TranscriptionResult(first.Text + second.Text);
	}

	public async Task StartThenShutdown()
	{
		EnsureConfigured();
		await _supervisor!.TranscribeAsync(Request(), CancellationToken.None);
		await _supervisor.StopAsync(CancellationToken.None);
	}

	public void AssertFreshGpuRetry() =>
		_factory!.StartedBackends.TakeLast(2).Should().Equal(ComputeBackend.Vulkan, ComputeBackend.Vulkan);

	public void AssertCpuRetry() =>
		_factory!.StartedBackends.Should().EndWith(ComputeBackend.Cpu);

	public void AssertResultReturned() => _result.Should().NotBeNull();

	public void AssertSupervisorAvailable() => _supervisor.Should().NotBeNull();

	public void AssertTypedFailure() => _failure.Should().BeOfType<InferenceUnavailableException>();

	public void AssertRecoveryBounded() => _factory!.StartedWorkers.Should().HaveCount(3);

	public void AssertSameGeneration()
	{
		_result!.Text.Should().Be("firstsecond");
		_factory!.StartedWorkers.Should().HaveCount(1);
		_supervisor!.CurrentGeneration.Should().Be(1);
	}

	public void AssertWorkerExited()
	{
		_factory!.StartedWorkers.Single().StopCount.Should().Be(1);
		_factory.StartedWorkers.Single().IsAlive.Should().BeFalse();
	}

	public void AssertNoOwnedWorker() => _supervisor!.HasLiveWorker.Should().BeFalse();

	private void Configure(bool gpuAvailable, params WorkerOutcome[] outcomes)
	{
		_gpuProbe.IsGpuRuntimeAvailableAsync(Arg.Any<CancellationToken>()).Returns(gpuAvailable);
		_factory = new ScenarioWorkerFactory(outcomes);
		_supervisor = new InferenceWorkerSupervisor(
			_factory,
			new GpuBackendSelector(_gpuProbe),
			NullLogger<InferenceWorkerSupervisor>.Instance);
	}

	private void EnsureConfigured() =>
		_supervisor.Should().NotBeNull("the scenario must configure a worker before acting");

	private static InferenceWorkerRequest Request() =>
		new("model.bin", "en", DecodingOptions.Default, 16_000, [0.1f]);

	public void Dispose() => _supervisor?.DisposeAsync().AsTask().GetAwaiter().GetResult();

	private sealed record WorkerOutcome(IReadOnlyList<object> Results)
	{
		public static WorkerOutcome Fail { get; } =
			new([new InferenceWorkerUnavailableException("worker unavailable")]);

		public static WorkerOutcome Success(params string[] values) =>
			new(values.Select(value => (object)new TranscriptionResult(value)).ToArray());

		public static WorkerOutcome Sequence(params object[] values) => new(values);
	}

	private sealed class ScenarioWorkerFactory(params WorkerOutcome[] outcomes) : IInferenceWorkerProcessFactory
	{
		private int _next;

		public List<ComputeBackend> StartedBackends { get; } = [];
		public List<ScenarioWorker> StartedWorkers { get; } = [];

		public ValueTask<IInferenceWorkerProcess> StartAsync(
			ComputeBackend backend,
			int generation,
			CancellationToken cancellationToken)
		{
			StartedBackends.Add(backend);
			ScenarioWorker worker = new(generation, backend, outcomes[_next++]);
			StartedWorkers.Add(worker);
			return ValueTask.FromResult<IInferenceWorkerProcess>(worker);
		}
	}

	private sealed class ScenarioWorker(int generation, ComputeBackend backend, WorkerOutcome outcome)
		: IInferenceWorkerProcess
	{
		private readonly Queue<object> _results = new(outcome.Results);

		public int Generation { get; } = generation;
		public int ProcessId => Generation;
		public ComputeBackend Backend { get; } = backend;
		public bool IsAlive { get; private set; } = true;
		public int StopCount { get; private set; }

		public ValueTask<TranscriptionResult> TranscribeAsync(
			InferenceWorkerRequest request,
			CancellationToken cancellationToken)
		{
			object result = _results.Dequeue();
			if (result is Exception failure)
			{
				IsAlive = false;
				throw failure;
			}

			return ValueTask.FromResult((TranscriptionResult)result);
		}

		public ValueTask StopAsync(CancellationToken cancellationToken)
		{
			StopCount++;
			IsAlive = false;
			return ValueTask.CompletedTask;
		}

		public ValueTask DisposeAsync()
		{
			IsAlive = false;
			return ValueTask.CompletedTask;
		}
	}
}
