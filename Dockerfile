# Build VertexBPMN.Api with .NET 9 and run as container
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "VertexBPMN.sln"
RUN dotnet publish "VertexBPMN.Api/VertexBPMN.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "VertexBPMN.Api.dll"]
