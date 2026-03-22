FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/DeployFlow.API/DeployFlow.API.csproj", "src/DeployFlow.API/"]
COPY ["src/DeployFlow.Application/DeployFlow.Application.csproj", "src/DeployFlow.Application/"]
COPY ["src/DeployFlow.Domain/DeployFlow.Domain.csproj", "src/DeployFlow.Domain/"]
COPY ["src/DeployFlow.Infrastructure/DeployFlow.Infrastructure.csproj", "src/DeployFlow.Infrastructure/"]
RUN dotnet restore "src/DeployFlow.API/DeployFlow.API.csproj"

COPY . .
WORKDIR "/src/src/DeployFlow.API"
RUN dotnet build "DeployFlow.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DeployFlow.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
# Install curl for healthcheck
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*
COPY --from=publish /app/publish .

# Non-root user for security
RUN groupadd -r appuser && useradd -r -g appuser appuser
USER appuser

ENTRYPOINT ["dotnet", "DeployFlow.API.dll"]
