FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["LLMGateway/LLMGateway.csproj", "LLMGateway/"]
COPY ["Shared.Auth/Shared.Auth.csproj", "Shared.Auth/"]
COPY ["Shared.Observability/Shared.Observability.csproj", "Shared.Observability/"]
RUN dotnet restore "LLMGateway/LLMGateway.csproj"
COPY . .
WORKDIR "/src/LLMGateway"
RUN dotnet publish "LLMGateway.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "LLMGateway.dll"]
