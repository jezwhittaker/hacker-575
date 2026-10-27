# Build context is the src/ directory: docker build -f api-cs/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY api-cs/OctoSupply.Api.csproj api-cs/
RUN dotnet restore api-cs/OctoSupply.Api.csproj
COPY api-cs/ api-cs/
RUN dotnet publish api-cs/OctoSupply.Api.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
# The migrator resolves ../database relative to the working directory.
WORKDIR /app/api
COPY --from=build /out .
COPY database/ /app/database/

ENV PORT=3000 \
    DB_FILE=/data/app.db
EXPOSE 3000

RUN mkdir -p /data
ENTRYPOINT ["dotnet", "OctoSupply.Api.dll"]
