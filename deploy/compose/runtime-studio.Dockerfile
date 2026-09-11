# Lean runtime image for VertexBPMN.Studio.
# Builds from a HOST-side `dotnet publish` output (deploy/compose/publish/studio) to
# keep the heavy solution build off the container disk — mirrors the FINAL stage of
# the repo's src/VertexBPMN.Studio/Dockerfile (base aspnet:10.0.11, non-root).
#
# Build context: deploy/compose/
#   docker build -f runtime-studio.Dockerfile -t vertexbpmn-studio:local .
FROM mcr.microsoft.com/dotnet/aspnet:10.0.11 AS base
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080

FROM base AS final
WORKDIR /app
COPY publish/studio/ ./
# Runtime must create state + plugin dirs as the non-root app user ($APP_UID);
# an empty named volume inherits the state dir's ownership on first mount.
RUN mkdir -p /var/lib/vertexbpmn \
    && chown -R $APP_UID /var/lib/vertexbpmn \
    && chown -R $APP_UID /app
USER $APP_UID
ENTRYPOINT ["dotnet", "VertexBPMN.Studio.dll"]
