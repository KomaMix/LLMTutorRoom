FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["ReviewService/ReviewService.csproj", "ReviewService/"]
COPY ["ReviewService.Contracts/ReviewService.Contracts.csproj", "ReviewService.Contracts/"]
RUN dotnet restore "ReviewService/ReviewService.csproj"

COPY . .
RUN dotnet publish "ReviewService/ReviewService.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
EXPOSE 8080
COPY --from=build /app/publish .
HEALTHCHECK --interval=10s --timeout=5s --retries=12 --start-period=10s \
    CMD curl --fail --silent --show-error http://127.0.0.1:8080/health/ready || exit 1
ENTRYPOINT ["dotnet", "ReviewService.dll"]
