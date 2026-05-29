FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY TravelBot/TravelBot.csproj TravelBot/
RUN dotnet restore TravelBot/TravelBot.csproj

COPY TravelBot/ TravelBot/
RUN dotnet publish TravelBot/TravelBot.csproj -c Release -o /app/out

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

COPY --from=build /app/out .

ENTRYPOINT ["dotnet", "TravelBot.dll"]
