namespace Querio.Api.Common.Endpoints;

/// <summary>
/// One implementation per feature slice. Endpoints register themselves, so adding a feature
/// never means editing a growing central routing file.
/// </summary>
public interface IEndpoint
{
    void MapEndpoint(IEndpointRouteBuilder endpoints);
}
