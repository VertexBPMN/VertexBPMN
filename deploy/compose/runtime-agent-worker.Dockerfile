FROM mcr.microsoft.com/dotnet/aspnet:10.0.11 AS base
WORKDIR /app
ENV DOTNET_EnableDiagnostics=0

FROM base AS final
WORKDIR /app
COPY publish/agent-worker/ ./
USER $APP_UID
ENTRYPOINT ["dotnet", "VertexBPMN.AgentWorker.dll"]
