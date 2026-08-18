using FluentValidation;
using Mediator;
using Querio.Domain.Common.Errors;
using ValidationException = Querio.Domain.Common.Errors.ValidationException;

namespace Querio.Application.Common.Behaviors;

/// <summary>
/// Runs every registered validator for a message before its handler sees it. Handlers can
/// therefore assume a well-formed request, and no handler has to remember to validate.
///
/// Failures are translated into the domain's own <see cref="ValidationException"/> rather
/// than FluentValidation's, so the API's exception handler stays unaware of the validation
/// library and emits ProblemDetails with an <c>errors</c> member.
/// </summary>
public sealed class ValidationBehavior<TMessage, TResponse>(IEnumerable<IValidator<TMessage>> validators)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : notnull, IMessage
{
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        var applicable = validators as IValidator<TMessage>[] ?? validators.ToArray();

        if (applicable.Length == 0)
        {
            return await next(message, cancellationToken);
        }

        var context = new ValidationContext<TMessage>(message);

        var results = await Task.WhenAll(
            applicable.Select(validator => validator.ValidateAsync(context, cancellationToken)));

        var failures = results
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .ToArray();

        if (failures.Length > 0)
        {
            // Group per property so the client can render errors against the right field.
            var errors = failures
                .GroupBy(failure => failure.PropertyName, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(failure => failure.ErrorMessage).Distinct(StringComparer.Ordinal).ToArray(),
                    StringComparer.Ordinal);

            throw new ValidationException(errors);
        }

        return await next(message, cancellationToken);
    }
}
