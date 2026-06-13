// Port for the ordered post-process pipeline. The pipeline behavior lives in
// Logic.AppManagement; this abstraction lets the transcription handler (Application) depend on the
// capability without referencing Logic. Implementations apply the configured steps — normalize, then
// the optional output transform — reading the live configuration so changes take effect next call.

namespace Application.Ports;

public interface IPostProcessor
{
	/// <summary>Runs the configured post-process steps over <paramref name="text"/> and returns the result.</summary>
	ValueTask<string> ProcessAsync(string text, CancellationToken cancellationToken);
}
