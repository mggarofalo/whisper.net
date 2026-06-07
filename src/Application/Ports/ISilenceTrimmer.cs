// Port for trimming trailing silence from a clip before transcription. The behavior lives in
// Logic.AudioManagement; this abstraction lets the handler depend on the capability, not the
// implementation.

using Domain.Audio;

namespace Application.Ports;

public interface ISilenceTrimmer
{
	AudioClip Trim(AudioClip clip);
}
