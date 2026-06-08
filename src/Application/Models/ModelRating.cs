// A coarse, three-level rating the model picker shows for a model's speed, accuracy, and memory use
// (WHISPER-27). Deliberately qualitative — the picker surfaces relative guidance ("this one is fast but
// less accurate"), not benchmark numbers — so the user can choose a model that fits their machine.

namespace Application.Models;

public enum ModelRating
{
	Low,
	Medium,
	High,
}
