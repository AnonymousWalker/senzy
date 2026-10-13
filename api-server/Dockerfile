FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json ./
COPY api-server/Senzy.Api.csproj api-server/
RUN dotnet restore api-server/Senzy.Api.csproj
COPY api-server/ api-server/
RUN dotnet publish api-server/Senzy.Api.csproj --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
USER root
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
RUN mkdir -p /home/app/.aspnet/DataProtection-Keys && chown -R $APP_UID:$APP_UID /home/app/.aspnet
COPY --from=build /app/publish ./
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Senzy.Api.dll"]
