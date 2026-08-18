using FluentValidation;
using Querio.Domain.Tenants;

namespace Querio.Application.Tenants.Invitations.InviteMember;

internal sealed class InviteMemberCommandValidator : AbstractValidator<InviteMemberCommand>
{
    public InviteMemberCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty()
            .WithMessage("Email address is required.")
            .MaximumLength(320)
            .WithMessage("Email address is too long.")
            .EmailAddress()
            .WithMessage("Enter a valid email address.");

        RuleFor(command => command.Role)
            .IsInEnum()
            .WithMessage("Choose a valid role.");
    }
}
