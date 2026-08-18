using System.Reflection;
using Querio.Application.Common.Behaviors;
using Querio.Domain.Common.Errors;

namespace Querio.Application.Tests;

/// <summary>
/// The layering is only real if something checks it. A stray <c>using</c> that drags ASP.NET
/// or EF Core into Domain compiles perfectly well and is easy to miss in review — these
/// tests turn that into a build failure.
/// </summary>
public sealed class ArchitectureTests
{
    private static readonly string[] ForbiddenInDomain =
    [
        "Microsoft.AspNetCore",
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
        "Mediator",
        "FluentValidation",
    ];

    /// <summary>
    /// Note what is absent: Microsoft.EntityFrameworkCore. Handlers query DbSets directly
    /// rather than through repositories, so Application must know EF. The boundary that
    /// earns its keep is the <em>provider</em> — Application must stay ignorant of Npgsql, so
    /// the database can be swapped without touching a use case.
    /// </summary>
    private static readonly string[] ForbiddenInApplication =
    [
        "Microsoft.AspNetCore",
        "Npgsql",
    ];

    [Fact]
    public void Domain_depends_on_nothing_but_the_framework()
    {
        var referenced = ReferencedAssemblyNames(typeof(QuerioException).Assembly);

        foreach (var forbidden in ForbiddenInDomain)
        {
            referenced.ShouldNotContain(
                name => name.StartsWith(forbidden, StringComparison.Ordinal),
                $"Querio.Domain must not reference {forbidden}.");
        }
    }

    [Fact]
    public void Application_does_not_reach_for_transport_or_persistence()
    {
        var referenced = ReferencedAssemblyNames(typeof(ValidationBehavior<,>).Assembly);

        foreach (var forbidden in ForbiddenInApplication)
        {
            referenced.ShouldNotContain(
                name => name.StartsWith(forbidden, StringComparison.Ordinal),
                $"Querio.Application must not reference {forbidden}; that belongs in Infrastructure or Api.");
        }
    }

    [Fact]
    public void Domain_does_not_reference_application()
    {
        var referenced = ReferencedAssemblyNames(typeof(QuerioException).Assembly);

        referenced.ShouldNotContain("Querio.Application");
        referenced.ShouldNotContain("Querio.Infrastructure");
        referenced.ShouldNotContain("Querio.Api");
    }

    [Fact]
    public void Application_does_not_reference_infrastructure_or_api()
    {
        var referenced = ReferencedAssemblyNames(typeof(ValidationBehavior<,>).Assembly);

        referenced.ShouldNotContain("Querio.Infrastructure");
        referenced.ShouldNotContain("Querio.Api");
    }

    [Fact]
    public void Reference_inspection_actually_sees_dependencies()
    {
        // Guards every other test in this class from passing vacuously: if the reference
        // list ever came back empty, all the "must not reference" assertions would succeed
        // while checking nothing.
        var referenced = ReferencedAssemblyNames(typeof(ValidationBehavior<,>).Assembly);

        referenced.ShouldContain("Querio.Domain");
        referenced.ShouldContain("FluentValidation");
    }

    private static string[] ReferencedAssemblyNames(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();
}
