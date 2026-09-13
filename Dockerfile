FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY CricaStudio.Api.csproj ./
RUN dotnet restore CricaStudio.Api.csproj

COPY . ./
RUN dotnet publish CricaStudio.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID

COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "CricaStudio.Api.dll"]
