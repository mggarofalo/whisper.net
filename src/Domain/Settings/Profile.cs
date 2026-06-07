// A named, switchable bundle of settings — e.g. a "Coding" profile with a different model and hotkey
// than the default. Identified by its name; immutable. Profile activation and app-rule matching are
// orchestrated in later modules, but the domain owns the shape and its non-empty-name invariant.

namespace Domain.Settings;

public sealed record Profile
{
	public string Name { get; }
	public AppSettings Settings { get; }

	public Profile(string name, AppSettings settings)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			throw new DomainException("Profile name must not be empty.");
		}

		Name = name;
		Settings = settings ?? throw new DomainException("Profile settings are required.");
	}
}
