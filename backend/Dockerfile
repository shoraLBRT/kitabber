# syntax=docker/dockerfile:1
# Images of the backend hosts (E01-03): one build, three targets. Context: the repository root.
#   docker build -f backend/Dockerfile --target api .      (also: workers, migrator)
# Base images are pinned by digest; Renovate updates tag and digest together.
ARG SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0@sha256:bef136a371963aaa46ec97882845ef6f54dec15728bb4b128b1c2a400ffbc9b4
ARG RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0@sha256:12b1135f5f8b3c0a214f2a680ff2033dff4f684f445d6c697d9524ceeb303586

FROM ${SDK_IMAGE} AS build
WORKDIR /src
ENV DOTNET_NOLOGO=true DOTNET_CLI_TELEMETRY_OPTOUT=true
COPY global.json Directory.Build.props ./
COPY backend/ backend/
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    for project in Api Workers Migrator; do \
        dotnet publish "backend/src/$project/Platform.$project.csproj" -c Release -o "/out/$project" --nologo -v q -p:UseAppHost=false || exit 1; \
    done

FROM ${RUNTIME_IMAGE} AS runtime
WORKDIR /app
# The image runs as the non-root user of the base image; settings and secrets come from the environment (ADR-0007).
USER $APP_UID

FROM runtime AS api
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /out/Api ./
ENTRYPOINT ["dotnet", "Platform.Api.dll"]

FROM runtime AS workers
COPY --from=build /out/Workers ./
ENTRYPOINT ["dotnet", "Platform.Workers.dll"]

FROM runtime AS migrator
COPY --from=build /out/Migrator ./
ENTRYPOINT ["dotnet", "Platform.Migrator.dll"]
CMD ["migrate"]
