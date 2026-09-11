// Typed terminal failure returned after the isolated inference worker has exhausted its bounded
// restart and CPU-fallback policy. The tray process remains alive; callers can surface this without
// leaking process/pipe/native implementation details into Application.

namespace Domain.Models;

public sealed class InferenceUnavailableException : Exception
{
	public InferenceUnavailableException(string message, Exception innerException)
		: base(message, innerException)
	{
	}
}
