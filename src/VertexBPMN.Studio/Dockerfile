#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json .
RUN dotnet --version
COPY ["VertexBPMN.Studio/VertexBPMN.Studio.csproj", "VertexBPMN.Studio/"]
RUN dotnet restore "VertexBPMN.Studio/VertexBPMN.Studio.csproj"
COPY . .
WORKDIR "/src/VertexBPMN.Studio"
RUN dotnet build "VertexBPMN.Studio.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "VertexBPMN.Studio.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "VertexBPMN.Studio.dll"]
