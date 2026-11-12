FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY backend/MymContratistas.Api/MymContratistas.Api.csproj backend/MymContratistas.Api/
RUN dotnet restore backend/MymContratistas.Api/MymContratistas.Api.csproj
COPY backend/MymContratistas.Api backend/MymContratistas.Api
RUN dotnet publish backend/MymContratistas.Api/MymContratistas.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .
# El frontend estático se sirve desde wwwroot
COPY frontend ./wwwroot
RUN mkdir -p Uploads
# Render indica el puerto en la variable PORT
ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-8080} exec dotnet MymContratistas.Api.dll"]
