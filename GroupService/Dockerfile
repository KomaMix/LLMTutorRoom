FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["GroupService/GroupService.csproj", "GroupService/"]
COPY ["Shared.Auth/Shared.Auth.csproj", "Shared.Auth/"]
COPY ["Shared.Observability/Shared.Observability.csproj", "Shared.Observability/"]
COPY ["GroupService.Contracts/GroupService.Contracts.csproj", "GroupService.Contracts/"]
RUN dotnet restore "GroupService/GroupService.csproj"

COPY . .
RUN dotnet publish "GroupService/GroupService.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "GroupService.dll"]
