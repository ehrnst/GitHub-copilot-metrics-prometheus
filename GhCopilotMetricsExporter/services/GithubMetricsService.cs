using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prometheus;
using System.Text.Json.Serialization;

namespace GhCopilotMetricsExporter.Services
{
    public class GitHubMetricsService : BackgroundService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<GitHubMetricsService> _logger;
        private readonly IConfiguration _configuration;
        private readonly Gauge _totalActiveUsers;
        private readonly Gauge _totalEngagedUsers;
        private readonly Gauge _totalSuggestions;
        private readonly Gauge _totalAcceptances;
        private readonly Gauge _totalLinesSuggested;
        private readonly Gauge _totalLinesAccepted;
        private readonly Gauge _totalChatEngagedUsers;
        private readonly Gauge _totalChats;

        public GitHubMetricsService(IHttpClientFactory httpClientFactory, ILogger<GitHubMetricsService> logger, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _configuration = configuration;

            _totalActiveUsers = Metrics.CreateGauge("github_copilot_total_active_users", "Total number of active users");
            _totalEngagedUsers = Metrics.CreateGauge("github_copilot_total_engaged_users", "Total number of engaged users");
            _totalSuggestions = Metrics.CreateGauge("github_copilot_total_suggestions", "Total number of code suggestions");
            _totalAcceptances = Metrics.CreateGauge("github_copilot_total_acceptances", "Total number of code acceptances");
            _totalLinesSuggested = Metrics.CreateGauge("github_copilot_total_lines_suggested", "Total number of lines suggested");
            _totalLinesAccepted = Metrics.CreateGauge("github_copilot_total_lines_accepted", "Total number of lines accepted");
            _totalChatEngagedUsers = Metrics.CreateGauge("github_copilot_total_chat_engaged_users", "Total number of chat engaged users");
            _totalChats = Metrics.CreateGauge("github_copilot_total_chats", "Total number of chats");
        }

        private readonly Gauge _suggestionsCountByLanguageEditor = Metrics.CreateGauge("github_copilot_suggestions_count_by_language_editor", "Suggestions count by language and editor", new GaugeConfiguration
        {
            LabelNames = new[] { "language", "editor" }
        });
        private readonly Gauge _acceptancesCountByLanguageEditor = Metrics.CreateGauge("github_copilot_acceptances_count_by_language_editor", "Acceptances count by language and editor", new GaugeConfiguration
        {
            LabelNames = new[] { "language", "editor" }
        });
        private readonly Gauge _linesSuggestedByLanguageEditor = Metrics.CreateGauge("github_copilot_lines_suggested_by_language_editor", "Lines suggested by language and editor", new GaugeConfiguration
        {
            LabelNames = new[] { "language", "editor" }
        });
        private readonly Gauge _linesAcceptedByLanguageEditor = Metrics.CreateGauge("github_copilot_lines_accepted_by_language_editor", "Lines accepted by language and editor", new GaugeConfiguration
        {
            LabelNames = new[] { "language", "editor" }
        });
        private readonly Gauge _activeUsersByLanguageEditor = Metrics.CreateGauge("github_copilot_active_users_by_language_editor", "Active users by language and editor", new GaugeConfiguration
        {
            LabelNames = new[] { "language", "editor" }
        });

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
                    var githubOrganizationName = Environment.GetEnvironmentVariable("GITHUB_ORGANIZATION_NAME");

                    _logger.LogInformation($"Querying {githubOrganizationName} for Copilot usage metrics...");

                    var client = _httpClientFactory.CreateClient();
                    client.DefaultRequestHeaders.Clear();
                    client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
                    client.DefaultRequestHeaders.Add("Authorization", $"token {githubToken}");
                    client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
                    client.DefaultRequestHeaders.Add("User-Agent", $"{githubOrganizationName}-copilot-metrics-exporter");


                    var response = await client.GetAsync($"https://api.github.com/orgs/{githubOrganizationName}/copilot/metrics", stoppingToken);

                    if (response.IsSuccessStatusCode)
                    {
                        var metrics = await response.Content.ReadFromJsonAsync<List<CopilotUsageMetrics>>(stoppingToken);
                        if (metrics != null)
                        {
                            var latestMetrics = metrics.OrderByDescending(m => m.Date).First();

                            _totalActiveUsers.Set(latestMetrics.TotalActiveUsers);
                            _totalEngagedUsers.Set(latestMetrics.TotalEngagedUsers);

                            // Aggregate IDE metrics
                            var ideMetrics = latestMetrics.CopilotIdeCodeCompletions;
                            int totalSuggestions = 0;
                            int totalAcceptances = 0;
                            int totalLinesSuggested = 0;
                            int totalLinesAccepted = 0;

                            foreach (var editor in ideMetrics.Editors)
                            {
                                foreach (var model in editor.Models)
                                {
                                    foreach (var lang in model.Languages)
                                    {
                                        // Update metrics with labels
                                        _suggestionsCountByLanguageEditor
                                            .WithLabels(lang.Name, editor.Name)
                                            .Set(lang.TotalCodeSuggestions);

                                        _acceptancesCountByLanguageEditor
                                            .WithLabels(lang.Name, editor.Name)
                                            .Set(lang.TotalCodeAcceptances);

                                        _linesSuggestedByLanguageEditor
                                            .WithLabels(lang.Name, editor.Name)
                                            .Set(lang.TotalCodeLinesSuggested);

                                        _linesAcceptedByLanguageEditor
                                            .WithLabels(lang.Name, editor.Name)
                                            .Set(lang.TotalCodeLinesAccepted);

                                        _activeUsersByLanguageEditor
                                            .WithLabels(lang.Name, editor.Name)
                                            .Set(lang.TotalEngagedUsers);

                                        // Also add to totals
                                        totalSuggestions += lang.TotalCodeSuggestions;
                                        totalAcceptances += lang.TotalCodeAcceptances;
                                        totalLinesSuggested += lang.TotalCodeLinesSuggested;
                                        totalLinesAccepted += lang.TotalCodeLinesAccepted;
                                    }
                                }
                            }

                            _totalSuggestions.Set(totalSuggestions);
                            _totalAcceptances.Set(totalAcceptances);
                            _totalLinesSuggested.Set(totalLinesSuggested);
                            _totalLinesAccepted.Set(totalLinesAccepted);

                            // Chat metrics
                            _totalChatEngagedUsers.Set(latestMetrics.CopilotIdeChat.TotalEngagedUsers);
                            int totalChats = latestMetrics.CopilotIdeChat.Editors
                                .SelectMany(e => e.Models)
                                .Sum(m => m.TotalChats);
                            _totalChats.Set(totalChats);
                        }
                    }
                    else
                    {
                        var responseBody = await response.Content.ReadAsStringAsync();
                        _logger.LogError($"Error querying GitHub API. Status Code: {response.StatusCode}, Response: {responseBody}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error querying GitHub API");
                }

                await Task.Delay(TimeSpan.FromHours(12), stoppingToken); // Adjust the interval as needed
            }
        }
    }

public class CopilotUsageMetrics
{
    [JsonPropertyName("date")]
    public string Date { get; set; }

    [JsonPropertyName("total_active_users")]
    public int TotalActiveUsers { get; set; }

    [JsonPropertyName("total_engaged_users")]
    public int TotalEngagedUsers { get; set; }

    [JsonPropertyName("copilot_ide_code_completions")]
    public IdeCodeCompletions CopilotIdeCodeCompletions { get; set; }

    [JsonPropertyName("copilot_ide_chat")]
    public IdeChat CopilotIdeChat { get; set; }
}

public class IdeCodeCompletions
{
    [JsonPropertyName("total_engaged_users")]
    public int TotalEngagedUsers { get; set; }

    [JsonPropertyName("editors")]
    public List<Editor> Editors { get; set; }
}

public class Editor
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("total_engaged_users")]
    public int TotalEngagedUsers { get; set; }

    [JsonPropertyName("models")]
    public List<Model> Models { get; set; }
}

public class Model
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("languages")]
    public List<Language> Languages { get; set; }

    [JsonPropertyName("total_engaged_users")]
    public int TotalEngagedUsers { get; set; }
}

public class Language
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("total_engaged_users")]
    public int TotalEngagedUsers { get; set; }

    [JsonPropertyName("total_code_suggestions")]
    public int TotalCodeSuggestions { get; set; }

    [JsonPropertyName("total_code_acceptances")]
    public int TotalCodeAcceptances { get; set; }

    [JsonPropertyName("total_code_lines_suggested")]
    public int TotalCodeLinesSuggested { get; set; }

    [JsonPropertyName("total_code_lines_accepted")]
    public int TotalCodeLinesAccepted { get; set; }
}

public class IdeChat
{
    [JsonPropertyName("total_engaged_users")]
    public int TotalEngagedUsers { get; set; }

    [JsonPropertyName("editors")]
    public List<ChatEditor> Editors { get; set; }
}

public class ChatEditor
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("models")]
    public List<ChatModel> Models { get; set; }
}

public class ChatModel
{
    [JsonPropertyName("total_chats")]
    public int TotalChats { get; set; }
}
}