# Use the SDK image for building the application
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src
COPY ["GhCopilotMetricsExporter/GhCopilotMetricsExporter.csproj", "GhCopilotMetricsExporter/"]
RUN dotnet restore "GhCopilotMetricsExporter/GhCopilotMetricsExporter.csproj"
COPY . .
WORKDIR "/src/GhCopilotMetricsExporter"
RUN dotnet publish "GhCopilotMetricsExporter.csproj" -c Release -o /app/publish

# Use the runtime image to run the application
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS final
WORKDIR /app

RUN adduser -D -u 1023 ghcopmetrics
RUN chown -R ghcopmetrics /app
USER 1023

COPY --from=build /app/publish .
COPY GhCopilotMetricsExporter/appsettings.json .
ENTRYPOINT ["dotnet", "GhCopilotMetricsExporter.dll"]