// Covers the remaining settings-related domain types — AppSettings, Profile, TransformDefinition,
// and ModelDescriptor — exercising their construction invariants and immutability/equality.

using AwesomeAssertions;
using Domain;
using Domain.Models;
using Domain.Settings;
using Xunit;

namespace Domain.Tests;

public sealed class SettingsTypesTests
{
	private static HotkeyBinding AnyHotkey => HotkeyBinding.Parse("Ctrl+Win");

	[Fact]
	public void Default_settings_are_valid()
	{
		AppSettings settings = AppSettings.Default;

		settings.ModelId.Should().NotBeNullOrWhiteSpace();
		settings.SilenceThresholdMs.Should().BeGreaterThanOrEqualTo(0);
	}

	[Fact]
	public void Settings_reject_an_empty_model_id()
	{
		Action creating = () => _ = new AppSettings("", AnyHotkey, 500, true);

		creating.Should().Throw<DomainException>();
	}

	[Fact]
	public void Settings_reject_a_negative_silence_threshold()
	{
		Action creating = () => _ = new AppSettings("base.en", AnyHotkey, -1, true);

		creating.Should().Throw<DomainException>();
	}

	[Fact]
	public void Settings_with_the_same_values_are_structurally_equal()
	{
		AppSettings a = new("base.en", AnyHotkey, 500, true);
		AppSettings b = new("base.en", AnyHotkey, 500, true);

		a.Should().Be(b);
		b.Should().NotBe(new AppSettings("base.en", AnyHotkey, 750, true));
	}

	[Fact]
	public void Profile_requires_a_name()
	{
		Action creating = () => _ = new Profile("", AppSettings.Default);

		creating.Should().Throw<DomainException>();
	}

	[Fact]
	public void Transform_requires_a_name_and_something_to_find()
	{
		Action noName = () => _ = new TransformDefinition("", "asap", "as soon as possible");
		Action noFind = () => _ = new TransformDefinition("expand", "", "as soon as possible");

		noName.Should().Throw<DomainException>();
		noFind.Should().Throw<DomainException>();
	}

	[Fact]
	public void Model_descriptor_rejects_empty_id_and_negative_size()
	{
		Action emptyId = () => _ = new ModelDescriptor("", "Base", 1);
		Action negativeSize = () => _ = new ModelDescriptor("base.en", "Base", -1);

		emptyId.Should().Throw<DomainException>();
		negativeSize.Should().Throw<DomainException>();
	}

	[Fact]
	public void Model_descriptors_with_the_same_values_are_equal()
	{
		new ModelDescriptor("base.en", "Base", 142).Should().Be(new ModelDescriptor("base.en", "Base", 142));
	}
}
