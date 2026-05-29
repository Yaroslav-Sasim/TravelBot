# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Копируем весь проект из папки StudentsTests
COPY TravelBot/ ./TravelBot/

# Переходим в папку проекта
WORKDIR /app/StudentsTests

# Восстанавливаем зависимости
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app/out

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

COPY --from=build /app/out .

ENTRYPOINT ["dotnet", "TravelBot.dll"]
