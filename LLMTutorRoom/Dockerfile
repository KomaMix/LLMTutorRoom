FROM node:22-bookworm-slim AS client-build
WORKDIR /src/LLMTutorRoom/ClientApp

COPY ["LLMTutorRoom/ClientApp/package.json", "LLMTutorRoom/ClientApp/package-lock.json", "./"]
RUN npm ci

COPY ["LLMTutorRoom/ClientApp/", "./"]
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["LLMTutorRoom/LLMTutorRoom.csproj", "LLMTutorRoom/"]
COPY ["Shared.Auth/Shared.Auth.csproj", "Shared.Auth/"]
COPY ["AttemptService.Contracts/AttemptService.Contracts.csproj", "AttemptService.Contracts/"]
COPY ["TeachingService.Contracts/TeachingService.Contracts.csproj", "TeachingService.Contracts/"]
COPY ["ReviewService.Contracts/ReviewService.Contracts.csproj", "ReviewService.Contracts/"]
RUN dotnet restore "LLMTutorRoom/LLMTutorRoom.csproj"

COPY . .
COPY --from=client-build /src/LLMTutorRoom/wwwroot LLMTutorRoom/wwwroot
RUN dotnet publish "LLMTutorRoom/LLMTutorRoom.csproj" -c Release -o /app/publish --no-restore -p:SkipClientBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "LLMTutorRoom.dll"]
