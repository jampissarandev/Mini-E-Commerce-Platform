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

COPY --from=build /app/publish .

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true

ENTRYPOINT ["dotnet", "MiniEcommerce.Api.dll"]
