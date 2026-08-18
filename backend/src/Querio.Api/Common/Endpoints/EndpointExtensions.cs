using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Querio.Api.Common.Endpoints;

internal static class EndpointExtensions
{
    /// <summary>
    /// Discovers every <see cref="IEndpoint"/> in the assembly. Scanning happens once at
    /// startup, not per request.
    /// </summary>
    public static IServiceCollection AddEndpoints(this IServiceCollection services, Assembly assembly)
    {
        var descriptors = assembly.DefinedTypes
            .Where(type => type is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false })
            .Where(type => type.IsAssignableTo(typeof(IEndpoint)))
            .Select(type => ServiceDescriptor.Transient(typeof(IEndpoint), type))
            .ToArray();

        services.TryAddEnumerable(descriptors);

        return services;
    }

    /// <summary>
    /// Maps every discovered endpoint. Pass a <paramref name="routeGroup"/> to apply shared
    /// conventions — a route prefix, authorization, rate limiting — to all of them at once.
    /// </summary>
    public static WebApplication MapEndpoints(this WebApplication app, RouteGroupBuilder? routeGroup = null)
    {
        IEndpointRouteBuilder builder = routeGroup is not null ? routeGroup : app;

        foreach (var endpoint in app.Services.GetRequiredService<IEnumerable<IEndpoint>>())
        {
            endpoint.MapEndpoint(builder);
        }

        return app;
    }
}
