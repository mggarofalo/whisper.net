// Configuration for the opt-in localhost AI rephrase feature (WHISPER-40). Privacy stance: rephrase is
// DISABLED by default and, when enabled, may only target a loopback endpoint (enforced by
// OllamaRephraseOptionsValidator). This is the single disclosed network entry point for transcript text
// — see README + CHANGELOG.

namespace Infrastructure.Rephrase;

public sealed class OllamaRephraseOptions
{
	public const string SectionName = "Rephrase";

	/// <summary>Master opt-in switch. Off by default: no rephrase request is ever made until the user enables it.</summary>
	public bool Enabled { get; set; }

	/// <summary>Ollama base endpoint. Must be loopback (localhost/127.0.0.1/[::1]); a remote host is rejected.</summary>
	public string Endpoint { get; set; } = "http://localhost:11434";

	/// <summary>The local Ollama model to rephrase with.</summary>
	public string Model { get; set; } = "llama3.2";

	/// <summary>How long to wait for the local model before treating the attempt as a (recoverable) failure.</summary>
	public int TimeoutSeconds { get; set; } = 30;
}
