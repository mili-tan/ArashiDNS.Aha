#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src
COPY ["ArashiDNS.Aha.csproj", "."]
RUN dotnet restore "./ArashiDNS.Aha.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "ArashiDNS.Aha.csproj" -c Release -o /app/build /p:UseAppHost=true /p:PublishAot=false

FROM build AS publish
RUN dotnet publish "ArashiDNS.Aha.csproj" -c Release -o /app/publish /p:UseAppHost=true /p:PublishAot=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ARASHI_ANY=1
ENV ARASHI_RUNNING_IN_CONTAINER=1
EXPOSE 16883
ENTRYPOINT ["dotnet", "ArashiDNS.Aha.dll"]
