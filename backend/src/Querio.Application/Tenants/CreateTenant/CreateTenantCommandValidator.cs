using FluentValidation;

namespace Querio.Application.Tenants.CreateTenant;

internal sealed class CreateTenantCommandValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantCommandValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty()
            .WithMessage("Organization name is required.")
            .MaximumLength(100)
            .WithMessage("Organization name must be 100 characters or fewer.");
    }
}
