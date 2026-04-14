FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["src/ProductHub.API/ProductHub.API.csproj", "src/ProductHub.API/"]
COPY ["src/ProductHub.Application/ProductHub.Application.csproj", "src/ProductHub.Application/"]
COPY ["src/ProductHub.Domain/ProductHub.Domain.csproj", "src/ProductHub.Domain/"]
COPY ["src/ProductHub.Infrastructure/ProductHub.Infrastructure.csproj", "src/ProductHub.Infrastructure/"]
COPY ["src/ProductHub.Shared/ProductHub.Shared.csproj", "src/ProductHub.Shared/"]

RUN dotnet restore "src/ProductHub.API/ProductHub.API.csproj"

COPY . .
WORKDIR "/src/src/ProductHub.API"
RUN dotnet build "ProductHub.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "ProductHub.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ProductHub.API.dll"]
