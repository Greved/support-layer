FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY platform/ ./
RUN dotnet restore src/Api.Portal/Api.Portal.csproj
RUN dotnet publish src/Api.Portal/Api.Portal.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 5001
ENV ASPNETCORE_URLS=http://+:5001

ENTRYPOINT ["dotnet", "Api.Portal.dll"]
