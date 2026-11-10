FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY api/api.csproj api/
COPY be/be.csproj be/
COPY database/database.csproj database/
COPY service/service.csproj service/
RUN dotnet restore api/api.csproj

COPY . .
RUN dotnet publish api/api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .

EXPOSE 8080

ENTRYPOINT ["dotnet", "api.dll"]
