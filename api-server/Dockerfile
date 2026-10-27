FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json ./
COPY api-server/Senzy.Api.csproj api-server/
COPY api-server/Modules/Ledger/Senzy.Modules.Ledger.csproj api-server/Modules/Ledger/
COPY api-server/Modules/Wallets/Senzy.Modules.Wallets.csproj api-server/Modules/Wallets/
COPY api-server/Modules/Payments/Senzy.Modules.Payments.csproj api-server/Modules/Payments/
COPY api-server/Modules/Funding/Senzy.Modules.Funding.csproj api-server/Modules/Funding/
COPY api-server/Infrastructure/Transactions/Senzy.Infrastructure.csproj api-server/Infrastructure/Transactions/
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
