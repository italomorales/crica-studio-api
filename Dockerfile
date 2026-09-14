FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY CricaStudio.Api/CricaStudio.Api.csproj CricaStudio.Api/
COPY CricaStudio.Application/CricaStudio.Application.csproj CricaStudio.Application/
COPY CricaStudio.Domain/CricaStudio.Domain.csproj CricaStudio.Domain/
COPY CricaStudio.Infrastructure/CricaStudio.Infrastructure.csproj CricaStudio.Infrastructure/
WORKDIR /src/CricaStudio.Api
RUN dotnet restore CricaStudio.Api.csproj

WORKDIR /src
COPY . ./
RUN dotnet publish CricaStudio.Api/CricaStudio.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID

COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "CricaStudio.Api.dll"]
