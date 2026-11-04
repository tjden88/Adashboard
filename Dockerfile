# Образ Adashboard: сервер ASP.NET Core, который отдаёт API и статику Blazor WASM-клиента.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Отдельный слой восстановления: копируем только slnx и csproj, поэтому при правках кода
# кэш NuGet сохраняется и пересобирается только код.
COPY Adashboard.slnx ./
COPY Adashboard.Client/Adashboard.Client.csproj Adashboard.Client/
COPY Adashboard.Data/Adashboard.Data.csproj Adashboard.Data/
COPY Adashboard.Domain/Adashboard.Domain.csproj Adashboard.Domain/
COPY Adashboard.Server/Adashboard.Server.csproj Adashboard.Server/
RUN dotnet restore Adashboard.slnx

# Каталог данных в образ не копируем (см. .dockerignore): в контейнере он — volume.
# Adashboard.Client/Generated не исключён из контекста: это сгенерированные Refit-клиенты, и без них
# клиент не собирается (перед сборкой образа их перегоняют scripts/generate-api-clients.ps1, если API менялся).
COPY . .

# Каталог ассетов в репозитории — «Assets», а DashboardPaths.AssetsPath и AssetsInitializer ищут каталог
# «assets» (строчными). На Linux имена регистрозависимы, поэтому на время публикации каталог
# Adashboard.Server/Assets переименовываем в строчное «assets».
RUN mv Adashboard.Server/Assets Adashboard.Server/assets
RUN dotnet publish Adashboard.Server/Adashboard.Server.csproj -c Release -o /src/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# В финальный образ — только результат публикации сервера (вместе с собранным WASM-клиентом).
COPY --from=build /src/publish ./

# dotnet publish не включает каталог Assets в результат публикации, а DashboardPaths.AssetsPath
# ищет каталог «assets» (строчными) рядом с content root. На Linux имена файлов регистрозависимы,
# поэтому в образе каталог называется именно /app/assets.
COPY Adashboard.Server/Assets ./assets

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
# Уровень логирования по умолчанию: переменную LogLevel можно переопределить при запуске контейнера.
ENV LogLevel=Information

ENTRYPOINT ["dotnet", "Adashboard.Server.dll"]
