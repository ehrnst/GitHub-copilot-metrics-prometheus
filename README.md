# GitHub Copilot Metrics Exporter Prometheus

A simple background worker written in .NET which will expose [Copilot usage data](https://docs.github.com/en/rest/copilot/copilot-usage?apiVersion=2022-11-28#get-a-summary-of-copilot-usage-for-organization-members) as Prometheus metrics. Once deployed to a Kubernetes cluster with scraping config, metrics should be available and can be viewed in Prometheus or Grafana.

## Features

- Collects GitHub Copilot usage metrics
- Exposes metrics via a Prometheus endpoint

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker](https://www.docker.com/get-started)

## Getting Started

### Configuration

Create your PAT token as per [GitHub's official documentation](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/managing-your-personal-access-tokens#creating-a-fine-grained-personal-access-token)

Update the `appsettings.json`, or create your own `appsettings.Development.json` file in the `GhCopilotMetricsExporter` directory with your GitHub token and organization name:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "GitHub": {
    "Token": "your-token",
    "Organization": "your-organization-name"
  }
}
```

## To do

[ ] Fix non hardcoded PAT tokens in the environment file
[ ] Provide YAML example for deployment
[ ] Create Grafana dashboard as Json