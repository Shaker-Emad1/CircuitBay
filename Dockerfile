FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY CircuitBay.slnx ./
COPY Directory.Build.props ./

COPY src/CircuitBay.Domain/CircuitBay.Domain.csproj src/CircuitBay.Domain/
COPY src/CircuitBay.Contracts/CircuitBay.Contracts.csproj src/CircuitBay.Contracts/
COPY src/CircuitBay.Application/CircuitBay.Application.csproj src/CircuitBay.Application/
COPY src/CircuitBay.Infrastructure/CircuitBay.Infrastructure.csproj src/CircuitBay.Infrastructure/
COPY src/CircuitBay.Api/CircuitBay.Api.csproj src/CircuitBay.Api/

COPY tests/CircuitBay.Tests/CircuitBay.Tests.csproj tests/CircuitBay.Tests/

RUN dotnet restore CircuitBay.slnx

COPY . .

RUN dotnet publish src/CircuitBay.Api/CircuitBay.Api.csproj -c Release -o /app/publish

FROM runtime AS final
WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "CircuitBay.Api.dll"]