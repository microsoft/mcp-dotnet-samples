# syntax=docker/dockerfile:1

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build

COPY ./src/McpTodoList.ContainerApp /source/McpTodoList.ContainerApp

WORKDIR /source/McpTodoList.ContainerApp

ARG TARGETARCH
RUN case "$TARGETARCH" in \
      "amd64") RID="linux-musl-x64" ;; \
      "arm64") RID="linux-musl-arm64" ;; \
      *) RID="linux-musl-x64" ;; \
    esac && \
    dotnet publish -c Release -o /app -r $RID --self-contained false

FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS final

WORKDIR /app

COPY --from=build /app .

USER $APP_UID

ENTRYPOINT ["dotnet", "McpTodoList.ContainerApp.dll"]
