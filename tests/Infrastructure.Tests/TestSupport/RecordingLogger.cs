// A minimal ILogger that records every entry's level and formatted message, so tests can assert that
// an adapter logged something (e.g. FileSettingsStore logging its recovery from a corrupt file).

using Microsoft.Extensions.Logging;

namespace Infrastructure.Tests.TestSupport;

public sealed class RecordingLogger<T> : ILogger<T>
{
	public List<(LogLevel Level, string Message)> Entries { get; } = [];

	public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

	public bool IsEnabled(LogLevel logLevel) => true;

	public void Log<TState>(
		LogLevel logLevel,
		EventId eventId,
		TState state,
		Exception? exception,
		Func<TState, Exception?, string> formatter) =>
		Entries.Add((logLevel, formatter(state, exception)));

	private sealed class NullScope : IDisposable
	{
		public static readonly NullScope Instance = new();

		public void Dispose()
		{
		}
	}
}
