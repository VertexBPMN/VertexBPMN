FROM mcr.microsoft.com/dotnet/aspnet:10.0.11 AS base
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0.302 AS build
WORKDIR /src
COPY . .
RUN dotnet --version
RUN dotnet restore "src/VertexBPMN.Studio/VertexBPMN.Studio.csproj"
RUN dotnet publish "src/VertexBPMN.Studio/VertexBPMN.Studio.csproj" -c Release -o /app/publish \
    --no-restore /p:UseAppHost=false /p:SkipBpmnIoAssetBuild=true

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "VertexBPMN.Studio.dll"]
