FROM mcr.microsoft.com/dotnet/sdk:11.0.100-rc.1-alpine3.24 AS build
WORKDIR /src
COPY global.json ./
COPY src/ ./src/
RUN dotnet publish src/Access.Api/Access.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:11.0.0-rc.1-alpine3.24
WORKDIR /app
COPY --from=build /app/publish ./
USER $APP_UID
ENTRYPOINT ["dotnet", "Access.Api.dll"]
