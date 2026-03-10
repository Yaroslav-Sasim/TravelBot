# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /app

# Копируем весь проект из папки StudentsTests
COPY StudentsTests/ ./StudentsTests/

# Переходим в папку проекта
WORKDIR /app/StudentsTests

# Восстанавливаем зависимости
RUN dotnet restore

# Публикуем проект
RUN dotnet publish -c Release -o /app/out

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Копируем опубликованные файлы
COPY --from=build /app/out .

ENTRYPOINT ["dotnet", "StudentsTests.dll"]