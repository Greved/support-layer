FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY platform/ ./
RUN dotnet restore src/Api.Public/Api.Public.csproj
RUN dotnet publish src/Api.Public/Api.Public.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000

ENTRYPOINT ["dotnet", "Api.Public.dll"]
