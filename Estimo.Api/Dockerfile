FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files first to leverage Docker layer caching
COPY Estimo.Api/Estimo.Api.csproj Estimo.Api/
COPY Estimo.Application/Estimo.Application.csproj Estimo.Application/
COPY Estimo.Infrastructure/Estimo.Infrastructure.csproj Estimo.Infrastructure/
COPY Estimo.Domain/Estimo.Domain.csproj Estimo.Domain/
RUN dotnet restore Estimo.Api/Estimo.Api.csproj

# Copy the remaining source and publish
COPY . .
RUN dotnet publish Estimo.Api/Estimo.Api.csproj -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Estimo.Api.dll"]
