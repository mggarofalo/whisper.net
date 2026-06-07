// FluentValidation rules for BrowseHistoryQuery (WHISPER-17), run by the ValidationBehavior pipeline
// before the handler: the page size must be within a sane positive range and the page must be at least
// one, so a negative page size or an out-of-range page is rejected with a clear error rather than
// reaching the store. Pure (no I/O).

using FluentValidation;

namespace Application.History;

public sealed class BrowseHistoryQueryValidator : AbstractValidator<BrowseHistoryQuery>
{
	private const int MaxPageSize = 200;

	public BrowseHistoryQueryValidator()
	{
		RuleFor(query => query.PageSize)
			.InclusiveBetween(1, MaxPageSize)
			.WithMessage($"Page size must be between 1 and {MaxPageSize}.");

		RuleFor(query => query.Page)
			.GreaterThanOrEqualTo(1)
			.WithMessage("Page must be 1 or greater.");
	}
}
