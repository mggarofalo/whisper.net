// Runs a test body on a dedicated STA thread (WHISPER-96): WPF elements demand single-threaded
// apartment affinity, and creating each test's visual objects on its own STA thread keeps the smoke
// layer deterministic without an application loop or an STA test-framework dependency. Failures are
// rethrown on the test thread with their original stack.

using System.Runtime.ExceptionServices;

namespace Presentation.Smoke.Tests;

internal static class StaThread
{
	public static void Run(Action body)
	{
		ExceptionDispatchInfo? failure = null;

		Thread thread = new(() =>
		{
			try
			{
				body();
			}
			catch (Exception exception)
			{
				failure = ExceptionDispatchInfo.Capture(exception);
			}
		})
		{
			IsBackground = true,
		};

		thread.SetApartmentState(ApartmentState.STA);
		thread.Start();
		thread.Join();

		failure?.Throw();
	}
}
