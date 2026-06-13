// Exercises the repository's own guidance + commit-hook conventions.
// Unlike the dictation drivers (which drive behavior through IMediator), this one inspects repository
// artifacts directly: it reads CLAUDE.md and runs the REAL .husky/commit-msg hook against sample
// messages, asserting at the same boundary a developer hits when committing.

using System.Diagnostics;
using AwesomeAssertions;

namespace Dictation.Specs.Drivers;

public sealed class RepositoryGuidanceDriver
{
	// Resolved once: the nearest ancestor of the test output directory that holds the solution file.
	private static readonly string RepositoryRoot = FindRepositoryRoot();

	private string _claudeMd = string.Empty;
	private int? _lastHookExitCode;

	public void OpenClaudeMd() =>
		_claudeMd = File.ReadAllText(Path.Combine(RepositoryRoot, "CLAUDE.md"));

	public void AssertClaudeMdPointsToAgentsMd()
	{
		_claudeMd.Should().Contain("AGENTS.md");
		_claudeMd.Should().Contain("canonical");
	}

	public void AssertCommitMsgHookInstalled() =>
		File.Exists(Path.Combine(RepositoryRoot, ".husky", "commit-msg")).Should().BeTrue();

	public void Commit(string message) =>
		_lastHookExitCode = RunCommitMsgHook(message);

	public void AssertCommitRejected() =>
		_lastHookExitCode.Should().NotBe(0, "a non-conventional commit message must be rejected by the hook");

	public void AssertCommitAccepted() =>
		_lastHookExitCode.Should().Be(0, "a conventional commit message must be accepted by the hook");

	// Runs the real commit-msg hook against a temp message file and returns its exit code (0 = accepted).
	private static int RunCommitMsgHook(string message)
	{
		string messageFile = Path.Combine(Path.GetTempPath(), $"whisper-commitmsg-{Guid.NewGuid():N}.txt");
		File.WriteAllText(messageFile, message + "\n");
		try
		{
			ProcessStartInfo startInfo = new()
			{
				FileName = FindPosixShell(),
				WorkingDirectory = RepositoryRoot,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
			};
			startInfo.ArgumentList.Add(Path.Combine(RepositoryRoot, ".husky", "commit-msg"));
			startInfo.ArgumentList.Add(messageFile);

			using Process process = Process.Start(startInfo)
				?? throw new InvalidOperationException("Failed to start the commit-msg hook process.");
			_ = process.StandardOutput.ReadToEnd();
			_ = process.StandardError.ReadToEnd();
			process.WaitForExit();
			return process.ExitCode;
		}
		finally
		{
			File.Delete(messageFile);
		}
	}

	private static string FindRepositoryRoot()
	{
		DirectoryInfo? directory = new(AppContext.BaseDirectory);
		while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Whisper.slnx")))
		{
			directory = directory.Parent;
		}

		return directory?.FullName
			?? throw new InvalidOperationException("Could not locate the repository root (Whisper.slnx).");
	}

	// A POSIX shell to run the hook. Prefer a known Git-for-Windows path when present (reliable even if
	// it isn't on PATH); otherwise fall back to "sh", which is on PATH on Linux and GitHub runners.
	private static string FindPosixShell()
	{
		string[] absoluteCandidates =
		[
			@"C:\Program Files\Git\usr\bin\sh.exe",
			@"C:\Program Files\Git\bin\sh.exe",
			@"C:\Program Files (x86)\Git\usr\bin\sh.exe",
		];

		foreach (string candidate in absoluteCandidates)
		{
			if (File.Exists(candidate))
			{
				return candidate;
			}
		}

		return "sh";
	}
}
