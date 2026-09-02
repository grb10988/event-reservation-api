FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY EventReservation.sln .
COPY src/api/EventReservation.Api.csproj src/api/
COPY src/application/EventReservation.Application.csproj src/application/
COPY src/domain/EventReservation.Domain.csproj src/domain/
COPY src/infrastructure/EventReservation.Infrastructure.csproj src/infrastructure/
RUN dotnet restore src/api/EventReservation.Api.csproj

COPY src/ src/
RUN dotnet publish src/api/EventReservation.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "EventReservation.Api.dll"]