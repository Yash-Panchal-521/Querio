using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Querio.Api.Tests.Api;

/// <summary>
/// Boots the real pipeline in-memory against a throwaway Postgres container. Integration
/// tests run the same middleware order and the same EF model production uses, so ordering and
/// mapping regressions surface here rather than on deploy.
/// </summary>
public sealed class QuerioApiFactory(
    string connectionString,
    TestTokenIssuer tokenIssuer,
    CapturingLogSink logSink) : WebApplicationFactory<Program>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);

        // Host configuration rather than services: these values are read while the
        // application builder is still being assembled, so replacing registrations
        // afterwards would be too late.
        builder.ConfigureHostConfiguration(configuration =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Querio"] = connectionString,
                ["Authentication:Firebase:ProjectId"] = TestTokenIssuer.ProjectId,
            }));

        builder.ConfigureServices(services =>
        {
            // ReadFrom.Services picks this up, so the sink sees exactly what production
            // logging would emit rather than a parallel test-only pipeline.
            services.AddSingleton<Serilog.Core.ILogEventSink>(logSink);

            // Supply the signing key directly instead of letting the handler fetch Google's
            // JWKS over the network. Everything else about validation — issuer, audience,
            // lifetime, algorithm — stays exactly as configured in production.
            services.PostConfigure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme,
                options =>
                {
                    options.Configuration = new OpenIdConnectConfiguration
                    {
                        Issuer = TestTokenIssuer.Issuer,
                    };

                    options.Configuration.SigningKeys.Add(tokenIssuer.SigningKey);

                    options.TokenValidationParameters.IssuerSigningKey = tokenIssuer.SigningKey;
                    options.TokenValidationParameters.ConfigurationManager = null;
                });
        });

        return base.CreateHost(builder);
    }
}
