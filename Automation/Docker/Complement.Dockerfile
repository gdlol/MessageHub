FROM docker.io/library/golang:1.18 AS build-libp2p
WORKDIR /messagehub-libp2p
COPY messagehub-libp2p ./
RUN --mount=type=cache,target=/go/pkg/mod/ \
    --mount=type=cache,target=/root/.cache/go-build \
    go build -buildmode=c-shared -v -o /root/lib/messagehub-libp2p.dll

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /MessageHub.Complement
COPY MessageHub/*.csproj /MessageHub/
COPY MessageHub.Complement/*.csproj ./
RUN dotnet restore
COPY MessageHub /MessageHub/
COPY MessageHub.Complement ./
RUN dotnet publish --configuration Release --output /root/app/

FROM mcr.microsoft.com/dotnet/aspnet:6.0
WORKDIR /root/app
COPY --from=build /root/app ./
COPY --from=build-libp2p /root/lib/messagehub-libp2p.dll ./
EXPOSE 8008 8448
ENTRYPOINT dotnet MessageHub.Complement.dll