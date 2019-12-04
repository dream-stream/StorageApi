FROM mcr.microsoft.com/dotnet/core/aspnet:3.0-buster-slim AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/core/sdk:3.0-buster AS build
WORKDIR /src
COPY ["Dream-Stream-StorageApi/Dream-Stream-StorageApi.csproj", "Dream-Stream-StorageApi/"]
RUN dotnet restore "Dream-Stream-StorageApi/Dream-Stream-StorageApi.csproj"
COPY . .
WORKDIR "/src/Dream-Stream-StorageApi"
RUN dotnet build "Dream-Stream-StorageApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Dream-Stream-StorageApi.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Dream-Stream-StorageApi.dll"]