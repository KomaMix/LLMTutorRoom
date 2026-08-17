FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["TeachingService/TeachingService.csproj", "TeachingService/"]
COPY ["Shared.Auth/Shared.Auth.csproj", "Shared.Auth/"]
COPY ["ReviewService.Contracts/ReviewService.Contracts.csproj", "ReviewService.Contracts/"]
COPY ["TeachingService.Contracts/TeachingService.Contracts.csproj", "TeachingService.Contracts/"]
RUN dotnet restore "TeachingService/TeachingService.csproj"

COPY . .
RUN dotnet publish "TeachingService/TeachingService.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TeachingService.dll"]
