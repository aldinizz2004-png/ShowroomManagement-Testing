FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Showroom.Api/Showroom.Api.csproj src/Showroom.Api/
RUN dotnet restore src/Showroom.Api/Showroom.Api.csproj

COPY src/Showroom.Api/ src/Showroom.Api/
RUN dotnet publish src/Showroom.Api/Showroom.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .

USER $APP_UID

ENTRYPOINT ["dotnet", "Showroom.Api.dll"]
