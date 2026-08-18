using FluentValidation;
using Mediator;

namespace Querio.Application.Tests.Common;

/// <summary>
/// Behaviours are generic over the message type, so they are exercised with purpose-built
/// messages here rather than with a real feature slice — that keeps the tests from breaking
/// every time a production command changes shape.
/// </summary>
internal sealed record IndexDocumentCommand(string FileName, int SizeInBytes) : ICommand<string>;

internal sealed class IndexDocumentCommandValidator : AbstractValidator<IndexDocumentCommand>
{
    public IndexDocumentCommandValidator()
    {
        RuleFor(command => command.FileName)
            .NotEmpty()
            .WithMessage("File name is required.");

        RuleFor(command => command.SizeInBytes)
            .GreaterThan(0)
            .WithMessage("File must not be empty.");
    }
}

/// <summary>A second validator for the same message, to prove failures are merged.</summary>
internal sealed class IndexDocumentCommandFileTypeValidator : AbstractValidator<IndexDocumentCommand>
{
    public IndexDocumentCommandFileTypeValidator()
    {
        RuleFor(command => command.FileName)
            .Must(fileName => string.IsNullOrEmpty(fileName) || fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Only PDF uploads are supported.");
    }
}
