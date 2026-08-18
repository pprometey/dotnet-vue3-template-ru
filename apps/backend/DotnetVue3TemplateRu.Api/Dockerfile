# Backend (DotnetVue3TemplateRu.Api) production image.
# Build context = repository root, because the Api references libs/backend/*.
#   docker build -f apps/backend/DotnetVue3TemplateRu.Api/Dockerfile -t dotnet-vue3-template-ru-api .

# --- build stage -----------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Shared build configuration: TargetFramework and analyzers (Directory.Build.props),
# Central Package Management versions (Directory.Packages.props), package sources
# (nuget.config), code style (.editorconfig).
COPY Directory.Build.props Directory.Packages.props nuget.config .editorconfig ./

# Backend sources. Copied whole, by directory, rather than as a list of individual
# .csproj paths: the module list grows, and a project missing from such a list is
# silently skipped by restore ("Skipping project ... because it was not found")
# instead of failing the build. The NuGet package cache is kept across builds by a
# BuildKit cache mount, so restore stays fast even though this layer is invalidated
# by any source change.
COPY apps/backend/ apps/backend/
COPY libs/backend/ libs/backend/

RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore apps/backend/DotnetVue3TemplateRu.Api/DotnetVue3TemplateRu.Api.csproj

RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish apps/backend/DotnetVue3TemplateRu.Api/DotnetVue3TemplateRu.Api.csproj \
    -c Release --no-restore -o /app/publish

# --- runtime stage ---------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Run as the non-root user shipped in the base image.
USER $APP_UID

# ASPNETCORE_ENVIRONMENT выбирает файл appsettings.<среда>.json; оркестратор
# переопределяет его на нужный стенд.
ENV ASPNETCORE_ENVIRONMENT=Prod \
    ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish ./

# Health endpoints (mapped by ServiceDefaults): /alive (liveness, no DB) and
# /health (readiness, requires DB). No Docker HEALTHCHECK here - the aspnet base
# image ships no curl/wget; the orchestrator (deploy pipeline / compose / k8s)
# probes these HTTP endpoints directly.

ENTRYPOINT ["dotnet", "DotnetVue3TemplateRu.Api.dll"]
