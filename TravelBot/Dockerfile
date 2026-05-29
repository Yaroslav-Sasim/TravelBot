# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app


COPY TravelBot/ ./TravelBot/


WORKDIR /app/TravelBot

# Восстанавливаем зависимостиRUN dotnet restore


RUN dotnet publish -c Release -o /app/out

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

COPY --from=build /app/out .

ENTRYPOINT ["dotnet", "TravelBot.dll"]
