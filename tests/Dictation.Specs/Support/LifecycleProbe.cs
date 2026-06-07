// Observes the Generic Host driving hosted services for the @WHISPER-12 scenarios: records that the
// host called StartAsync and StopAsync on a registered IHostedService, so the specs can assert the
// hosted-service lifecycle generically rather than depending on a specific production component.

namespace Dictation.Specs.Support;

public sealed class LifecycleProbe
{
	public bool Started { get; set; }

	public bool Stopped { get; set; }
}
