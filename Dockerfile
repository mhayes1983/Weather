# dotnet publish builds the client through weather.client.esproj, so the SDK stage needs Node.
FROM node:24-slim AS node

FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
COPY --from=node /usr/local/bin/node /usr/local/bin/node
COPY --from=node /usr/local/lib/node_modules /usr/local/lib/node_modules
RUN ln -s ../lib/node_modules/npm/bin/npm-cli.js /usr/local/bin/npm
WORKDIR /src
COPY . .
RUN dotnet publish Weather.Server/Weather.Server.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble
# DefaultLocation:TimeZoneId needs time zone data.
RUN apt-get update \
    && apt-get install -y --no-install-recommends tzdata \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Weather.Server.dll"]
