FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/TaskManager/TaskManager.Shared/TaskManager.Shared.csproj", "TaskManager.Shared/"]
COPY ["src/TaskManager/TaskManager/TaskManager.Client/TaskManager.Client.csproj", "TaskManager/TaskManager.Client/"]
COPY ["src/TaskManager/TaskManager/TaskManager/TaskManager.csproj", "TaskManager/TaskManager/"]

RUN dotnet restore "TaskManager/TaskManager/TaskManager.csproj"

COPY src/TaskManager/TaskManager.Shared/ TaskManager.Shared/
COPY src/TaskManager/TaskManager/TaskManager.Client/ TaskManager/TaskManager.Client/
COPY src/TaskManager/TaskManager/TaskManager/ TaskManager/TaskManager/

WORKDIR /src/TaskManager/TaskManager
RUN dotnet publish "TaskManager.csproj" \
    --configuration Release \
    --output /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

USER root
RUN apt-get update \
    && apt-get install --yes --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://0.0.0.0:10000
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 10000

COPY --from=build /app/publish .

USER $APP_UID
ENTRYPOINT ["dotnet", "TaskManager.dll"]
