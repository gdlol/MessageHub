FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /MessageHub.ElementServer
COPY MessageHub.ElementServer/*.csproj ./
RUN dotnet restore
COPY MessageHub.ElementServer ./
RUN dotnet publish --configuration Release --output /root/app/

FROM mcr.microsoft.com/dotnet/aspnet:6.0
WORKDIR /root/app
COPY --from=build /root/app ./
COPY --from=docker.io/vectorim/element-web /app ./Clients/Element/
COPY config.json ./
ENTRYPOINT dotnet MessageHub.ElementServer.dll
