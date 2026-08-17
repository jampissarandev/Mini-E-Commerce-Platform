# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY MiniEcommerce.Api/*.csproj ./MiniEcommerce.Api/
RUN dotnet restore MiniEcommerce.Api/MiniEcommerce.Api.csproj

COPY . .
RUN dotnet publish MiniEcommerce.Api/MiniEcommerce.Api.csproj -c Release -o /app/publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl for the container healthcheck
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Non-root user
RUN useradd --create-home appuser

COPY --from=build /app/publish .

# wwwroot for LocalImageStorage — WebRootPath must resolve and appuser must be able to write
RUN mkdir -p /app/wwwroot/images \
    && chown -R appuser:appuser /app

USER appuser

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true

HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
  CMD curl -fsS http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "MiniEcommerce.Api.dll"]
