using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Querio.Application.Common.Abstractions;
using Querio.Infrastructure.Persistence;
using Querio.Infrastructure.Embeddings;
using Querio.Infrastructure.Extraction;
using Querio.Infrastructure.Ingestion;
using Querio.Infrastructure.Persistence.Interceptors;
using Querio.Infrastructure.Storage;

namespace Querio.Infrastructure;

public static class DependencyInjection
{
    private const string ConnectionStringName = "Querio";

    /// <summary>
    /// Composition root for everything that talks to the outside world — Postgres, object
    /// storage, the embedding and chat providers, and the ingestion worker.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString(ConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // Fail at start-up with a sentence that says what to do, rather than at the first
            // request with a null-reference somewhere inside EF.
            throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not configured. Set ConnectionStrings:{ConnectionStringName} "
                + "in appsettings.local.json (copy appsettings.local.example.json) or the environment.");
        }

        services.TryAddSingletonTimeProvider();

        services.AddSingleton<AuditableEntityInterceptor>();

        services.AddDbContext<QuerioDbContext>((serviceProvider, options) =>
        {
            options
                .UseNpgsql(connectionString, npgsql =>
                {
                    npgsql.MigrationsAssembly(typeof(QuerioDbContext).Assembly.FullName);
                    npgsql.UseVector();
                    npgsql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorCodesToAdd: null);
                })
                // Postgres is case-folding and snake_case by convention; matching it keeps
                // hand-written SQL and psql sessions free of quoted identifiers.
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(serviceProvider.GetRequiredService<AuditableEntityInterceptor>());
        });

        services.AddScoped<IQuerioDbContext>(serviceProvider =>
            serviceProvider.GetRequiredService<QuerioDbContext>());

        // One instance per unit of work, exposed twice: read-only to everything, settable only
        // through ITenantScope. A request establishes it after proving membership; the
        // ingestion worker establishes it after claiming a job.
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(serviceProvider => serviceProvider.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantScope>(serviceProvider => serviceProvider.GetRequiredService<TenantContext>());

        services.AddObjectStorage(configuration);

        services.AddEmbeddings(configuration);

        services.AddIngestion(configuration);

        // One per format, resolved by IEnumerable and selected on Format. Adding a format is
        // then a new class and nothing else.
        services.AddSingleton<ITextExtractor, PlainTextExtractor>();
        services.AddSingleton<ITextExtractor, MarkdownTextExtractor>();
        services.AddSingleton<ITextExtractor, PdfTextExtractor>();
        services.AddSingleton<ITextExtractor, WordTextExtractor>();

        // Singleton on purpose: the point of the cache is that it outlives the scoped health
        // check instances the probe creates on every poll.
        services.AddSingleton<SchemaReadinessCache>();

        services.AddHealthChecks()
            .AddCheck<PendingMigrationsHealthCheck>(
                name: "database",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready"]);

        return services;
    }

    private static void TryAddSingletonTimeProvider(this IServiceCollection services)
    {
        // Tests swap in FakeTimeProvider; nothing else should construct DateTimeOffset.UtcNow.
        if (services.All(descriptor => descriptor.ServiceType != typeof(TimeProvider)))
        {
            services.AddSingleton(TimeProvider.System);
        }
    }

    private static void AddObjectStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(ObjectStorageOptions.SectionName);

        var options = new ObjectStorageOptions
        {
            ServiceUrl = section[nameof(ObjectStorageOptions.ServiceUrl)] ?? string.Empty,
            AccessKeyId = section[nameof(ObjectStorageOptions.AccessKeyId)] ?? string.Empty,
            SecretAccessKey = section[nameof(ObjectStorageOptions.SecretAccessKey)] ?? string.Empty,
            BucketName = section[nameof(ObjectStorageOptions.BucketName)] ?? string.Empty,
        };

        if (section[nameof(ObjectStorageOptions.Region)] is { Length: > 0 } region)
        {
            options.Region = region;
        }

        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(options.ServiceUrl))
        {
            missing.Add(nameof(ObjectStorageOptions.ServiceUrl));
        }

        if (string.IsNullOrWhiteSpace(options.AccessKeyId))
        {
            missing.Add(nameof(ObjectStorageOptions.AccessKeyId));
        }

        if (string.IsNullOrWhiteSpace(options.SecretAccessKey))
        {
            missing.Add(nameof(ObjectStorageOptions.SecretAccessKey));
        }

        if (string.IsNullOrWhiteSpace(options.BucketName))
        {
            missing.Add(nameof(ObjectStorageOptions.BucketName));
        }

        if (missing.Count > 0)
        {
            // Checked while the host is still being built, not at the first upload. A
            // deployment missing its storage settings then fails before the readiness probe
            // ever passes, instead of accepting somebody's file and losing it.
            throw new InvalidOperationException(
                $"Object storage is not configured. Missing: {string.Join(", ", missing.Select(name => $"{ObjectStorageOptions.SectionName}:{name}"))}. "
                + "Set them in appsettings.local.json (copy appsettings.local.example.json) or the environment.");
        }

        services.AddSingleton(Options.Create(options));

        services.AddSingleton(_ => ObjectStorageClientFactory.Create(options));

        services.AddScoped<IDocumentStorage, S3DocumentStorage>();
    }

    private static void AddEmbeddings(this IServiceCollection services, IConfiguration configuration)
    {
        // Read from the parent section, not from either provider's, because it decides which of
        // them is even configured. Defaulting to the hosted provider keeps existing deployments
        // behaving exactly as they did before this setting existed.
        var provider = configuration["Embeddings:Provider"] is { Length: > 0 } configured
            && Enum.TryParse<EmbeddingProvider>(configured, ignoreCase: true, out var parsed)
                ? parsed
                : EmbeddingProvider.Gemini;

        switch (provider)
        {
            case EmbeddingProvider.Ollama:
                services.AddOllamaEmbeddings(configuration);

                break;

            case EmbeddingProvider.Cloudflare:
                services.AddCloudflareEmbeddings(configuration);

                break;

            default:
                services.AddGeminiEmbeddings(configuration);

                break;
        }
    }

    private static void AddOllamaEmbeddings(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(OllamaEmbeddingOptions.SectionName);
        var options = new OllamaEmbeddingOptions();

        if (section[nameof(OllamaEmbeddingOptions.BaseAddress)] is { Length: > 0 } baseAddress)
        {
            options.BaseAddress = baseAddress;
        }

        if (section[nameof(OllamaEmbeddingOptions.Model)] is { Length: > 0 } model)
        {
            options.Model = model;
        }

        if (section[nameof(OllamaEmbeddingOptions.ModelIdentity)] is { Length: > 0 } identity)
        {
            options.ModelIdentity = identity;
        }

        options.BatchSize = ReadInt(section, nameof(OllamaEmbeddingOptions.BatchSize), options.BatchSize);
        options.TimeoutSeconds = ReadInt(section, nameof(OllamaEmbeddingOptions.TimeoutSeconds), options.TimeoutSeconds);

        // No key to check and no allowance to configure. The only way this provider is
        // misconfigured is that Ollama is not running, which its own error says plainly.
        services.AddSingleton(Options.Create(options));

        services.AddHttpClient<IEmbeddingService, OllamaEmbeddingService>(client =>
        {
            client.BaseAddress = new Uri(options.BaseAddress);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });
    }

    private static void AddCloudflareEmbeddings(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(CloudflareEmbeddingOptions.SectionName);
        var options = new CloudflareEmbeddingOptions();

        if (section[nameof(CloudflareEmbeddingOptions.AccountId)] is { Length: > 0 } accountId)
        {
            options.AccountId = accountId;
        }

        if (section[nameof(CloudflareEmbeddingOptions.ApiToken)] is { Length: > 0 } apiToken)
        {
            options.ApiToken = apiToken;
        }

        if (section[nameof(CloudflareEmbeddingOptions.Model)] is { Length: > 0 } model)
        {
            options.Model = model;
        }

        if (section[nameof(CloudflareEmbeddingOptions.ModelIdentity)] is { Length: > 0 } identity)
        {
            options.ModelIdentity = identity;
        }

        if (section[nameof(CloudflareEmbeddingOptions.Pooling)] is { Length: > 0 } pooling)
        {
            options.Pooling = pooling;
        }

        if (section[nameof(CloudflareEmbeddingOptions.BaseAddress)] is { Length: > 0 } baseAddress)
        {
            options.BaseAddress = baseAddress;
        }

        options.BatchSize = ReadInt(section, nameof(CloudflareEmbeddingOptions.BatchSize), options.BatchSize);
        options.MaxInputTokens = ReadInt(section, nameof(CloudflareEmbeddingOptions.MaxInputTokens), options.MaxInputTokens);
        options.TimeoutSeconds = ReadInt(section, nameof(CloudflareEmbeddingOptions.TimeoutSeconds), options.TimeoutSeconds);

        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(options.AccountId))
        {
            missing.Add(nameof(CloudflareEmbeddingOptions.AccountId));
        }

        if (string.IsNullOrWhiteSpace(options.ApiToken))
        {
            missing.Add(nameof(CloudflareEmbeddingOptions.ApiToken));
        }

        if (missing.Count > 0)
        {
            // Checked while the host is still being built. An instance that starts without these
            // would accept an upload and fail it at the embedding step, having already stored the
            // bytes and told the user it was working on them.
            throw new InvalidOperationException(
                $"Workers AI is not configured. Missing: {string.Join(", ", missing.Select(name => $"{CloudflareEmbeddingOptions.SectionName}:{name}"))}. "
                + "The account id is not a secret; the token needs Workers AI read access. "
                + "For local work without any allowance, set Embeddings:Provider to Ollama instead.");
        }

        services.AddSingleton(Options.Create(options));

        services.AddHttpClient<IEmbeddingService, CloudflareEmbeddingService>(client =>
        {
            client.BaseAddress = new Uri(options.BaseAddress);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.ApiToken);
        });
    }

    private static void AddGeminiEmbeddings(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(GeminiEmbeddingOptions.SectionName);
        var options = new GeminiEmbeddingOptions();

        if (section[nameof(GeminiEmbeddingOptions.ApiKey)] is { Length: > 0 } apiKey)
        {
            options.ApiKey = apiKey;
        }

        if (section[nameof(GeminiEmbeddingOptions.Model)] is { Length: > 0 } model)
        {
            options.Model = model;
        }

        if (section[nameof(GeminiEmbeddingOptions.BaseAddress)] is { Length: > 0 } baseAddress)
        {
            options.BaseAddress = baseAddress;
        }

        options.BatchSize = ReadInt(section, nameof(GeminiEmbeddingOptions.BatchSize), options.BatchSize);
        options.RequestsPerMinute = ReadInt(section, nameof(GeminiEmbeddingOptions.RequestsPerMinute), options.RequestsPerMinute);
        options.TokensPerMinute = ReadInt(section, nameof(GeminiEmbeddingOptions.TokensPerMinute), options.TokensPerMinute);
        options.PassagesPerDay = ReadInt(section, nameof(GeminiEmbeddingOptions.PassagesPerDay), options.PassagesPerDay);
        options.MaxAttempts = ReadInt(section, nameof(GeminiEmbeddingOptions.MaxAttempts), options.MaxAttempts);

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException(
                $"Embedding provider is not configured. Set {GeminiEmbeddingOptions.SectionName}:{nameof(GeminiEmbeddingOptions.ApiKey)} "
                + "in appsettings.local.json (copy appsettings.local.example.json) or the environment. "
                + "A free key comes from https://aistudio.google.com. "
                + "For local work without an allowance, set Embeddings:Provider to Ollama instead.");
        }

        services.AddSingleton(Options.Create(options));

        // Singleton on purpose: the ceilings are the process's, not one job's. See the type.
        services.AddSingleton<EmbeddingAllowance>();

        services.AddHttpClient<IEmbeddingService, GeminiEmbeddingService>(
            client => GeminiEmbeddingClientFactory.Configure(client, options));
    }

    private static int ReadInt(IConfiguration section, string key, int fallback) =>
        int.TryParse(section[key], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;

    private static void AddIngestion(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(IngestionOptions.SectionName);

        var options = new IngestionOptions
        {
            IdlePollSeconds = ReadInt(section, nameof(IngestionOptions.IdlePollSeconds), 5),
            LeaseSeconds = ReadInt(section, nameof(IngestionOptions.LeaseSeconds), 120),
            Enabled = !bool.TryParse(section[nameof(IngestionOptions.Enabled)], out var enabled) || enabled,
        };

        services.AddSingleton(Options.Create(options));
        services.AddScoped<DocumentIngestionPipeline>();
        services.AddScoped<IngestionJobRunner>();
        services.AddHostedService<IngestionWorker>();
    }
}
