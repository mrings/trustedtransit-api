FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app
COPY . .
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/out .
EXPOSE 8080

# Disable file watcher
ENV DOTNET_USE_POLLING_FILE_WATCHER=true
ENV DOTNET_WATCH_SUPPRESS_BROWSER_REFRESH=1
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

CMD ["dotnet", "TrustedTransit.Api.dll"]