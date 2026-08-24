using FluentValidation;

namespace Querio.Application.Documents.ListDocumentChunks;

internal sealed class ListDocumentChunksQueryValidator : AbstractValidator<ListDocumentChunksQuery>
{
    public const int MaxTake = 200;

    public ListDocumentChunksQueryValidator()
    {
        RuleFor(query => query.Skip)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Skip cannot be negative.");

        RuleFor(query => query.Take)
            .InclusiveBetween(1, MaxTake)
            .WithMessage($"Take must be between 1 and {MaxTake}.");
    }
}
