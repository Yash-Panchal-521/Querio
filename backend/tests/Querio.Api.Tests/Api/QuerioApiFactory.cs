using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Querio.Application.Common.Abstractions;

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
    /// <summary>
    /// Exposed so tests can assert that files were actually stored and removed, not only that
    /// rows were.
    /// </summary>
    public InMemoryDocumentStorage DocumentStorage { get; } = new();

    public FakeEmbeddingService Embeddings { get; } = new();

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

                // Placeholders. Storage settings are validated at start-up, but nothing in
                // these tests resolves the client — the storage implementation is exercised
                // directly against a real MinIO container in S3DocumentStorageTests.
                ["ObjectStorage:ServiceUrl"] = "http://object-storage.invalid",
                ["ObjectStorage:AccessKeyId"] = "test-access-key",
                ["ObjectStorage:SecretAccessKey"] = "test-secret-key",
                ["ObjectStorage:BucketName"] = "querio-test",

                // Placeholder for the same reason as the storage settings above: the provider
                // is validated at start-up and nothing in these tests calls it. The real client
                // is exercised in GeminiEmbeddingServiceTests, and against the live API in
                // GeminiEmbeddingLiveTests when a key is configured.
                ["Embeddings:Gemini:ApiKey"] = "test-api-key",

                // The background loop is off in tests. Racing a timer to observe a state
                // transition produces the kind of test that passes locally and fails in CI for
                // reasons nobody can reproduce — the runner is driven directly instead.
                ["Ingestion:Enabled"] = "false",
            }));

        builder.ConfigureServices(services =>
        {
            // ReadFrom.Services picks this up, so the sink sees exactly what production
            // logging would emit rather than a parallel test-only pipeline.
            services.AddSingleton<Serilog.Core.ILogEventSink>(logSink);

            // The real S3 implementation is covered against a live MinIO container in
            // S3DocumentStorageTests. Endpoint tests are about status codes, isolation and
            // permissions, so they use a fake and stay hermetic.
            services.RemoveAll<IDocumentStorage>();
            services.AddSingleton<IDocumentStorage>(DocumentStorage);

            // No network and no allowance spent. The real client has its own tests, including
            // live ones against the provider when a key is configured.
            services.RemoveAll<IEmbeddingService>();
            services.AddSingleton<IEmbeddingService>(Embeddings);

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
