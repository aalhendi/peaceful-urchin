FROM mcr.microsoft.com/dotnet/sdk:11.0.100-rc.1-alpine3.24
WORKDIR /src
COPY global.json peaceful-urchin.sln ./
COPY src/ ./src/
COPY tests/ ./tests/
CMD ["dotnet", "test", "peaceful-urchin.sln"]
