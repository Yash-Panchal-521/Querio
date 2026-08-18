# syntax=docker/dockerfile:1

# Render has no native .NET runtime, so the API ships as a container. The build context is
# `backend/`, which is also the Root Directory configured on the Render service.

# ---- build ------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Restore gets its own layer, keyed only on the files that can change what is restored.
# Editing a .cs file then leaves the package cache intact and the rebuild is seconds.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Querio.Domain/Querio.Domain.csproj src/Querio.Domain/
COPY src/Querio.Application/Querio.Application.csproj src/Querio.Application/
COPY src/Querio.Infrastructure/Querio.Infrastructure.csproj src/Querio.Infrastructure/
COPY src/Querio.Api/Querio.Api.csproj src/Querio.Api/

# The Api project rather than the solution: restoring the solution would also pull the test
# projects' Testcontainers and xUnit graph, which the runtime image never sees.
RUN dotnet restore src/Querio.Api/Querio.Api.csproj

COPY src/ src/
RUN dotnet publish src/Querio.Api/Querio.Api.csproj \
        --configuration Release \
        --no-restore \
        --output /app

# ---- runtime ----------------------------------------------------------------------------
# Not a chiseled image. InvariantGlobalization is deliberately false (see
# Directory.Build.props) so the runtime needs ICU, which the chiseled base omits.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app ./

# Serilog's file sink writes under ./logs. Ownership has to be handed over before the drop to
# non-root, or building the logger throws and the process dies during start-up.
RUN mkdir -p /app/logs && chown -R $APP_UID:$APP_UID /app/logs

# The aspnet images define app (UID 1654) for exactly this.
USER $APP_UID

# Matches Render's default injected PORT, and keeps a plain `docker run` usable.
ENV ASPNETCORE_URLS=http://0.0.0.0:10000
EXPOSE 10000

ENTRYPOINT ["dotnet", "Querio.Api.dll"]
