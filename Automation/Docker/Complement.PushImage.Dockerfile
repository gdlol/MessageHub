FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /Complement
COPY /Automation/Complement/*.csproj ./
RUN dotnet restore
COPY /Automation/Complement ./
RUN dotnet publish \
    --configuration Release \
    --self-contained \
    --runtime linux-musl-x64 \
    --output /root/app/

FROM docker.io/library/docker
RUN apk add --no-cache \
    libgcc \
    libintl \
    libssl1.1 \
    libstdc++ \
    zlib
ENV DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true
COPY . /root/project/
WORKDIR /root/app
COPY --from=build /root/app ./
ENTRYPOINT ./Complement
