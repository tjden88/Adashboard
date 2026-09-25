# syntax=docker/dockerfile:1

# Сборка приложения.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Сначала только файл проекта: слой restore кэшируется, пока не менялись зависимости.
COPY Adashboard/Adashboard.csproj Adashboard/
RUN dotnet restore Adashboard/Adashboard.csproj

COPY . .
RUN dotnet publish Adashboard/Adashboard.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# Образ времени выполнения.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Единый каталог изменяемых данных (база данных и пользовательские загрузки) внутри volume.
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    DASHBOARD_DATA_PATH=/data \
    DOTNET_EnableDiagnostics=0

COPY --from=build /app/publish .

# Каталог данных создаём до объявления volume, чтобы том унаследовал владельца app.
RUN mkdir -p /data && chown -R app:app /data
VOLUME ["/data"]

USER app
EXPOSE 8080

ENTRYPOINT ["dotnet", "Adashboard.dll"]
