FROM golang:1.18 AS build-libp2p
RUN apt-get update
RUN apt-get install -y gcc-mingw-w64
ENV GOOS=windows
ENV GOARCH=amd64
ENV CGO_ENABLED=1
ENV CC=x86_64-w64-mingw32-gcc
WORKDIR /messagehub-libp2p
COPY messagehub-libp2p/ ./
RUN --mount=type=cache,target=/go/pkg/mod/ \
    --mount=type=cache,target=/root/.cache/go-build \
    go build -buildmode=c-shared -v -o /root/lib/messagehub-libp2p.dll

FROM mcr.microsoft.com/dotnet/aspnet:6.0
WORKDIR /root/app/
COPY --from=build-libp2p /root/lib/messagehub-libp2p.dll ./
COPY --from=vectorim/element-web /app ./Clients/Element/
COPY config.json ./
CMD [ "cp", "-r", ".", "/root/build/MessageHub" ]
