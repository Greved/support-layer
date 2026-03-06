FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY platform/ ./
RUN dotnet restore src/Api.Admin/Api.Admin.csproj
RUN dotnet publish src/Api.Admin/Api.Admin.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 5002
ENV ASPNETCORE_URLS=http://+:5002

ENTRYPOINT ["dotnet", "Api.Admin.dll"]
