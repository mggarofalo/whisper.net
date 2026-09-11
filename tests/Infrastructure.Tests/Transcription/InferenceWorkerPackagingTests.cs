using AwesomeAssertions;
using Xunit;

namespace Infrastructure.Tests.Transcription;

public sealed class InferenceWorkerPackagingTests
{
	private static readonly string RepositoryRoot = FindRepositoryRoot();

	[Fact]
	public void Tray_build_and_release_package_include_the_isolated_worker()
	{
		string presentation = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "Presentation", "Presentation.csproj"));
		string pack = File.ReadAllText(Path.Combine(RepositoryRoot, "build", "pack.ps1"));
		string infrastructure = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "Infrastructure", "Infrastructure.csproj"));

		presentation.Should().Contain("Inference.Worker.csproj");
		presentation.Should().Contain("CopyInferenceWorker");
		pack.Should().Contain("dotnet publish $workerProject");
		pack.Should().Contain("$workerPublishDir");
		infrastructure.Should().NotContain("Whisper.net.Runtime");
	}

	private static string FindRepositoryRoot()
	{
		DirectoryInfo? directory = new(AppContext.BaseDirectory);
		while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Whisper.slnx")))
		{
			directory = directory.Parent;
		}

		return directory?.FullName
			?? throw new InvalidOperationException("Could not locate the repository root.");
	}
}
