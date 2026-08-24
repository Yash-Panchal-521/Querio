using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Querio.Application.Common.Abstractions;
using Querio.Application.Common.Behaviors;
using Querio.Application.Documents.Chunking;

namespace Querio.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the CQRS pipeline and every validator in this assembly.
    ///
    /// Behaviour order is the execution order: logging wraps validation, so a rejected
    /// request is still recorded with its duration rather than vanishing.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediator(options =>
        {
            options.Namespace = "Querio.Application";
            options.ServiceLifetime = ServiceLifetime.Scoped;
            options.PipelineBehaviors =
            [
                typeof(RequestLoggingBehavior<,>),
                typeof(ValidationBehavior<,>),
            ];
        });

        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly(), includeInternalTypes: true);

        // Chunking is pure logic with no external dependency, so it lives here rather than in
        // Infrastructure — which also means it can be tested without a container.
        services.AddSingleton<IChunker, StructureAwareChunker>();

        return services;
    }
}
