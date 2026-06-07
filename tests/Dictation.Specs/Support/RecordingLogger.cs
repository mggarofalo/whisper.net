// A minimal ILogger that records every entry's level and formatted message, so specs can assert that
// a component logged something (e.g. the settings store logging its recovery from a corrupt file).
// Generic over the category type so it can be injected wherever an ILogger<T> is required.

using Microsoft.Extensions.Logging;

namespace Dictation.Specs.Support;

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
