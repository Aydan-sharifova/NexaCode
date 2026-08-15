FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /source
COPY ["global.json", "Coding.sln", "./"]
COPY ["src/Coding.Domain/Coding.Domain.csproj", "src/Coding.Domain/"]
COPY ["src/Coding.Application/Coding.Application.csproj", "src/Coding.Application/"]
COPY ["src/Coding.Infrastructure/Coding.Infrastructure.csproj", "src/Coding.Infrastructure/"]
COPY ["src/Coding.Api/Coding.Api.csproj", "src/Coding.Api/"]
RUN dotnet restore "src/Coding.Api/Coding.Api.csproj"
COPY . .
RUN dotnet publish "src/Coding.Api/Coding.Api.csproj" \
    -c Release --no-restore -o /out \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app
RUN apk add --no-cache icu-libs \
    && mkdir -p /app/wwwroot/uploads/avatars \
    && chown -R app:app /app
COPY --from=build --chown=app:app /out .
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
EXPOSE 8080
ENTRYPOINT ["dotnet", "Coding.Api.dll"]
