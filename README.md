# GitHub Copilot Metrics Exporter Prometheus

A simple background worker written in .NET which will expose [Copilot usage data](https://docs.github.com/en/rest/copilot/copilot-usage?apiVersion=2022-11-28#get-a-summary-of-copilot-usage-for-organization-members) as Prometheus metrics. Once deployed to a Kubernetes cluster with scraping config, metrics should be available and can be viewed in Prometheus or Grafana.

## Features

- Collects GitHub Copilot usage metrics
- Exposes metrics via a Prometheus endpoint

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker](https://www.docker.com/get-started)

## Getting Started

### Deploy

Create your PAT token as per [GitHub's official documentation](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/managing-your-personal-access-tokens#creating-a-fine-grained-personal-access-token)

Create a secret in your k8s namespace with values for organization and PAT token
`kubectl create secret generic github-copilot-secrets --from-literal=token=ghp_**** --from-literal=organization=Adatum-no -n <namespace>`

Chose which manifest file you want to deploy. One uses Azure's custom `PodMonitor` or you can use the default deployment with Prometheus scraping annotations.

### Grafana

A simple Grafana dashboard is provided as a starting point.