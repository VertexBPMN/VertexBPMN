# Build VertexBPMN.AgentWorker with pinned .NET 10 images and run as the built-in non-root user.
FROM mcr.microsoft.com/dotnet/aspnet:10.0.11 AS base
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0.302 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "src/VertexBPMN.AgentWorker/VertexBPMN.AgentWorker.csproj"
RUN dotnet publish "src/VertexBPMN.AgentWorker/VertexBPMN.AgentWorker.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "VertexBPMN.AgentWorker.dll"]
