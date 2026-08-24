using FluentValidation;
using Querio.Domain.Documents;

namespace Querio.Application.Documents.UploadDocument;

internal sealed class UploadDocumentCommandValidator : AbstractValidator<UploadDocumentCommand>
{
    public UploadDocumentCommandValidator()
    {
        RuleFor(command => command.FileName)
            .NotEmpty()
            .WithMessage("A file name is required.")
            .MaximumLength(Document.MaxFileNameLength)
            .WithMessage($"File names must be {Document.MaxFileNameLength} characters or fewer.");

        RuleFor(command => command.Content)
            .NotNull()
            .WithMessage("No file was uploaded.");
    }
}
