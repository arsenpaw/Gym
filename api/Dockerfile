FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so the package layer is cached until a project file changes.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/FitnessClub.Domain/FitnessClub.Domain.csproj src/FitnessClub.Domain/
COPY src/FitnessClub.Application/FitnessClub.Application.csproj src/FitnessClub.Application/
COPY src/FitnessClub.Infrastructure/FitnessClub.Infrastructure.csproj src/FitnessClub.Infrastructure/
COPY src/FitnessClub.Api/FitnessClub.Api.csproj src/FitnessClub.Api/
RUN dotnet restore src/FitnessClub.Api/FitnessClub.Api.csproj

COPY src/ src/
RUN dotnet publish src/FitnessClub.Api/FitnessClub.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
# curl is only used by the compose health check.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "FitnessClub.Api.dll"]
