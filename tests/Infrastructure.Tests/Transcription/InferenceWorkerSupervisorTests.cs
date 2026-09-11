using Application.Ports;
using AwesomeAssertions;
using Domain.Audio;
using Domain.Models;
using Infrastructure.Transcription;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Infrastructure.Tests.Transcription;

public sealed class InferenceWorkerSupervisorTests
{
	private readonly IBackendSelector _backendSelector = Substitute.For<IBackendSelector>();

	public InferenceWorkerSupervisorTests() =>
		_backendSelector.SelectBackendAsync(Arg.Any<CancellationToken>())
			.Returns(new BackendSelection(ComputeBackend.Vulkan, "test GPU"));

	[Fact]
	public async Task Reuses_a_healthy_worker_generation()
	{
		ScriptedWorkerFactory factory = new(
			WorkerPlan.Succeeds("first", "second"));
		await using InferenceWorkerSupervisor supervisor = CreateSupervisor(factory);

		TranscriptionResult first = await supervisor.TranscribeAsync(Request(), CancellationToken.None);
		TranscriptionResult second = await supervisor.TranscribeAsync(Request(), CancellationToken.None);

		first.Text.Should().Be("first");
		second.Text.Should().Be("second");
		factory.StartedBackends.Should().Equal(ComputeBackend.Vulkan);
		supervisor.CurrentGeneration.Should().Be(1);
	}

	[Fact]
	public async Task Replaces_a_failed_gpu_worker_and_retries_the_same_request()
	{
		ScriptedWorkerFactory factory = new(
			WorkerPlan.Fails(),
			WorkerPlan.Succeeds("recovered"));
		await using InferenceWorkerSupervisor supervisor = CreateSupervisor(factory);

		TranscriptionResult result = await supervisor.TranscribeAsync(Request(), CancellationToken.None);

		result.Text.Should().Be("recovered");
		factory.StartedBackends.Should().Equal(ComputeBackend.Vulkan, ComputeBackend.Vulkan);
		factory.StartedWorkers[0].DisposeCount.Should().Be(1);
		supervisor.CurrentGeneration.Should().Be(2);
	}

	[Fact]
	public async Task Uses_cpu_when_the_backend_selector_reports_the_gpu_gone_during_recovery()
	{
		_backendSelector.SelectBackendAsync(Arg.Any<CancellationToken>())
			.Returns(
				new BackendSelection(ComputeBackend.Vulkan, "test GPU"),
				new BackendSelection(ComputeBackend.Cpu, "GPU gone"));
		ScriptedWorkerFactory factory = new(
			WorkerPlan.Fails(),
			WorkerPlan.Succeeds("cpu"));
		await using InferenceWorkerSupervisor supervisor = CreateSupervisor(factory);

		TranscriptionResult result = await supervisor.TranscribeAsync(Request(), CancellationToken.None);

		result.Text.Should().Be("cpu");
		factory.StartedBackends.Should().Equal(ComputeBackend.Vulkan, ComputeBackend.Cpu);
	}

	[Fact]
	public async Task Forces_cpu_when_a_replacement_gpu_worker_also_fails()
	{
		ScriptedWorkerFactory factory = new(
			WorkerPlan.Fails(),
			WorkerPlan.Fails(),
			WorkerPlan.Succeeds("cpu fallback"));
		await using InferenceWorkerSupervisor supervisor = CreateSupervisor(factory);

		TranscriptionResult result = await supervisor.TranscribeAsync(Request(), CancellationToken.None);

		result.Text.Should().Be("cpu fallback");
		factory.StartedBackends.Should().Equal(
			ComputeBackend.Vulkan,
			ComputeBackend.Vulkan,
			ComputeBackend.Cpu);
	}

	[Fact]
	public async Task Stops_after_cpu_failure_and_returns_a_typed_error()
	{
		ScriptedWorkerFactory factory = new(
			WorkerPlan.Fails(),
			WorkerPlan.Fails(),
			WorkerPlan.Fails());
		await using InferenceWorkerSupervisor supervisor = CreateSupervisor(factory);

		Func<Task> act = async () => await supervisor.TranscribeAsync(Request(), CancellationToken.None);

		await act.Should().ThrowAsync<InferenceUnavailableException>();
		factory.StartedWorkers.Should().HaveCount(3);
		supervisor.HasLiveWorker.Should().BeFalse();
	}

	[Fact]
	public async Task Graceful_stop_terminates_and_releases_the_owned_worker()
	{
		ScriptedWorkerFactory factory = new(WorkerPlan.Succeeds("ready"));
		await using InferenceWorkerSupervisor supervisor = CreateSupervisor(factory);
		await supervisor.TranscribeAsync(Request(), CancellationToken.None);

		await supervisor.StopAsync(CancellationToken.None);

		factory.StartedWorkers[0].StopCount.Should().Be(1);
		factory.StartedWorkers[0].DisposeCount.Should().Be(1);
		supervisor.HasLiveWorker.Should().BeFalse();
	}

	private InferenceWorkerSupervisor CreateSupervisor(IInferenceWorkerProcessFactory factory) =>
		new(factory, _backendSelector, NullLogger<InferenceWorkerSupervisor>.Instance);

	private static InferenceWorkerRequest Request() =>
		new("model.bin", "en", DecodingOptions.Default, 16_000, [0.1f, 0.2f]);

	private sealed record WorkerPlan(IReadOnlyList<object> Outcomes)
	{
		public static WorkerPlan Succeeds(params string[] text) =>
			new(text.Select(value => (object)new TranscriptionResult(value)).ToArray());

		public static WorkerPlan Fails() =>
			new([new InferenceWorkerUnavailableException("worker failed")]);
	}

	private sealed class ScriptedWorkerFactory(params WorkerPlan[] plans) : IInferenceWorkerProcessFactory
	{
		private int _nextPlan;

		public List<ComputeBackend> StartedBackends { get; } = [];
		public List<ScriptedWorker> StartedWorkers { get; } = [];

		public ValueTask<IInferenceWorkerProcess> StartAsync(
			ComputeBackend backend,
			int generation,
			CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			StartedBackends.Add(backend);
			ScriptedWorker worker = new(generation, backend, plans[_nextPlan++]);
			StartedWorkers.Add(worker);
			return ValueTask.FromResult<IInferenceWorkerProcess>(worker);
		}
	}

	private sealed class ScriptedWorker(int generation, ComputeBackend backend, WorkerPlan plan) : IInferenceWorkerProcess
	{
		private readonly Queue<object> _outcomes = new(plan.Outcomes);

		public int Generation { get; } = generation;
		public int ProcessId => Generation;
		public ComputeBackend Backend { get; } = backend;
		public bool IsAlive { get; private set; } = true;
		public int StopCount { get; private set; }
		public int DisposeCount { get; private set; }

		public ValueTask<TranscriptionResult> TranscribeAsync(
			InferenceWorkerRequest request,
			CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			object outcome = _outcomes.Dequeue();
			if (outcome is Exception failure)
			{
				IsAlive = false;
				throw failure;
			}

			return ValueTask.FromResult((TranscriptionResult)outcome);
		}

		public ValueTask StopAsync(CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			StopCount++;
			IsAlive = false;
			return ValueTask.CompletedTask;
		}

		public ValueTask DisposeAsync()
		{
			DisposeCount++;
			IsAlive = false;
			return ValueTask.CompletedTask;
		}
	}
}
