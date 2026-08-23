FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["AttemptService/AttemptService.csproj", "AttemptService/"]
COPY ["AttemptService.Contracts/AttemptService.Contracts.csproj", "AttemptService.Contracts/"]
COPY ["ReviewService.Contracts/ReviewService.Contracts.csproj", "ReviewService.Contracts/"]
COPY ["Shared.Auth/Shared.Auth.csproj", "Shared.Auth/"]
COPY ["TeachingService.Contracts/TeachingService.Contracts.csproj", "TeachingService.Contracts/"]
RUN dotnet restore "AttemptService/AttemptService.csproj"

COPY . .
RUN dotnet publish "AttemptService/AttemptService.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
EXPOSE 8080
COPY --from=build /app/publish .
HEALTHCHECK --interval=10s --timeout=5s --retries=10 \
    CMD curl --fail --silent http://localhost:8080/health/ready || exit 1
ENTRYPOINT ["dotnet", "AttemptService.dll"]
