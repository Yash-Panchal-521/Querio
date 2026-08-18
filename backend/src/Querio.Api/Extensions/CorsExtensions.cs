namespace Querio.Api.Extensions;

internal static class CorsExtensions
{
    internal const string PolicyName = "QuerioFrontend";

    /// <summary>
    /// Allows only the origins listed under <c>Cors:AllowedOrigins</c>. Credentials are
    /// permitted because the frontend sends a Firebase bearer token, and the SSE answer
    /// stream needs its headers exposed.
    ///
    /// In Development any loopback origin is accepted as well, because the dev server lands
    /// on whatever port is free and pinning a list means a silent CORS failure the first time
    /// 3000 is already taken. This relaxation is environment-gated: outside Development the
    /// configured list is the whole story.
    /// </summary>
    public static WebApplicationBuilder AddQuerioCors(this WebApplicationBuilder builder)
    {
        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? [];

        var allowLoopback = builder.Environment.IsDevelopment();

        builder.Services.AddCors(options => options.AddPolicy(PolicyName, policy =>
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()
                .WithExposedHeaders("Content-Disposition");

            if (allowLoopback)
            {
                // A wildcard is not an option here: the CORS spec forbids "*" alongside
                // credentials, so the origin has to be inspected and echoed back.
                policy.SetIsOriginAllowed(IsLoopbackOrigin);
            }
        }));

        return builder;
    }

    private static bool IsLoopbackOrigin(string origin) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri)
        && (uri.IsLoopback || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase));
}
