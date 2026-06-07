// Unit tests for the settings update validator (WHISPER-46): the field rules it enforces — known
// model id, non-empty parseable hotkey, sane silence threshold — and that a valid update passes.
// These are the branches behind the @WHISPER-46 "rejected before persistence" scenario.

using Application.Settings;
using FluentValidation.Results;
using Xunit;

namespace Application.Tests.Settings;

public sealed class UpdateSettingsCommandValidatorTests
{
	private readonly UpdateSettingsCommandValidator _validator = new();

	private static UpdateSettingsCommand Command(
		string modelId = "base.en",
		string hotkey = "Ctrl+Win",
		int silenceMs = 500) =>
		new(new AppSettingsDto(modelId, hotkey, silenceMs, FillerWordRemovalEnabled: true));

	[Fact]
	public void A_valid_update_passes()
	{
		ValidationResult result = _validator.Validate(Command());

		Assert.True(result.IsValid);
	}

	[Theory]
	[InlineData("totally-not-a-model")]
	[InlineData("")]
	public void An_unknown_or_empty_model_id_fails(string modelId)
	{
		ValidationResult result = _validator.Validate(Command(modelId: modelId));

		Assert.False(result.IsValid);
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void An_empty_hotkey_fails(string hotkey)
	{
		ValidationResult result = _validator.Validate(Command(hotkey: hotkey));

		Assert.False(result.IsValid);
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(60_001)]
	public void A_silence_threshold_out_of_range_fails(int silenceMs)
	{
		ValidationResult result = _validator.Validate(Command(silenceMs: silenceMs));

		Assert.False(result.IsValid);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(60_000)]
	public void A_silence_threshold_at_the_bounds_passes(int silenceMs)
	{
		ValidationResult result = _validator.Validate(Command(silenceMs: silenceMs));

		Assert.True(result.IsValid);
	}
}
