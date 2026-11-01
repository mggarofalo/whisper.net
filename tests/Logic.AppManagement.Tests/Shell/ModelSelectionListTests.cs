// Inner TDD loop for choosing a model from the selection list (WHISPER-149), WPF-free.

using Application.Models;
using AwesomeAssertions;
using Logic.AppManagement.Shell;
using Mediator;
using NSubstitute;
using Xunit;

namespace Logic.AppManagement.Tests.Shell;

public sealed class ModelSelectionListTests
{
	[Fact]
	public async Task Choosing_the_persisted_default_before_it_is_downloaded_downloads_it_then_switches()
	{
		IMediator mediator = Substitute.For<IMediator>();
		mediator.Send(Arg.Any<ListModelsQuery>(), Arg.Any<CancellationToken>())
			.Returns(new ValueTask<IReadOnlyList<ModelListItemDto>>(
				[new ModelListItemDto("base.en", "Base (EN)", 100, default, default, default, IsDownloaded: false, IsActive: true)]));
		mediator.Send(Arg.Any<DownloadModelCommand>(), Arg.Any<CancellationToken>()).Returns(new ValueTask<Unit>(Unit.Value));
		mediator.Send(Arg.Any<SwitchActiveModelCommand>(), Arg.Any<CancellationToken>()).Returns(new ValueTask<Unit>(Unit.Value));

		ModelViewModel viewModel = new(mediator);
		await viewModel.LoadCommand.ExecuteAsync(null);
		ModelItemViewModel row = viewModel.Models.Single();
		row.StatusText.Should().Be("Not downloaded", "a model is only in use once it is on this device");

		await viewModel.SelectCommand.ExecuteAsync(row);

		await mediator.Received(1).Send(Arg.Any<DownloadModelCommand>(), Arg.Any<CancellationToken>());
		await mediator.Received(1).Send(Arg.Is<SwitchActiveModelCommand>(c => c.ModelId == "base.en"), Arg.Any<CancellationToken>());
		row.StatusText.Should().Be("In use");
		viewModel.ActiveModel.Should().BeSameAs(row);
	}
}
