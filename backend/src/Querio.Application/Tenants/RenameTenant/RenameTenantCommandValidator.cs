using FluentValidation;

namespace Querio.Application.Tenants.RenameTenant;

internal sealed class RenameTenantCommandValidator : AbstractValidator<RenameTenantCommand>
{
    public RenameTenantCommandValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty()
            .WithMessage("Organization name is required.")
            .MaximumLength(100)
            .WithMessage("Organization name must be 100 characters or fewer.");
    }
}
