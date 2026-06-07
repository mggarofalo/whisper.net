// Drives the @WHISPER-36 normalization scenarios against the REAL Logic.AudioManagement behavior:
// the IFillerWordCleaner resolved from DI. No model or pipeline is involved — the normalizer is a
// pure function, so the driver simply feeds it raw text plus the "remove filler words" toggle and
// asserts on the normalized result.

using Application.Ports;
using AwesomeAssertions;

namespace Dictation.Specs.Drivers;

public sealed class TranscriptionNormalizationDriver(IFillerWordCleaner cleaner)
{
	// The app default: filler removal on. Scenarios that care flip it explicitly in a Given.
	private bool _removeFillerWords = true;
	private string _raw = string.Empty;
	private string _normalized = string.Empty;

	public void SetFillerRemoval(bool on) => _removeFillerWords = on;

	public void SetRawTranscription(string raw) => _raw = raw;

	public void Normalize() => _normalized = cleaner.Clean(_raw, _removeFillerWords);

	public void AssertNormalized(string expected) => _normalized.Should().Be(expected);
}
