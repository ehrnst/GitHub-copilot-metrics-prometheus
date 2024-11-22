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
        private readonly Gauge _totalSuggestionsCount;
        private readonly Gauge _totalAcceptancesCount;
        private readonly Gauge _totalLinesSuggested;
        private readonly Gauge _totalLinesAccepted;
        private readonly Gauge _totalActiveUsers;
        private readonly Gauge _totalChatAcceptances;
        private readonly Gauge _totalChatTurns;
        private readonly Gauge _totalActiveChatUsers;

        public GitHubMetricsService(IHttpClientFactory httpClientFactory, ILogger<GitHubMetricsService> logger, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _configuration = configuration;

            _totalSuggestionsCount = Metrics.CreateGauge("github_copilot_total_suggestions_count", "Total number of Copilot suggestions.");
            _totalAcceptancesCount = Metrics.CreateGauge("github_copilot_total_acceptances_count", "Total number of Copilot acceptances.");
            _totalLinesSuggested = Metrics.CreateGauge("github_copilot_total_lines_suggested", "Total number of lines suggested by Copilot.");
            _totalLinesAccepted = Metrics.CreateGauge("github_copilot_total_lines_accepted", "Total number of lines accepted by Copilot.");
            _totalActiveUsers = Metrics.CreateGauge("github_copilot_total_active_users", "Total number of active users.");
            _totalChatAcceptances = Metrics.CreateGauge("github_copilot_total_chat_acceptances", "Total number of chat acceptances.");
            _totalChatTurns = Metrics.CreateGauge("github_copilot_total_chat_turns", "Total number of chat turns.");
            _totalActiveChatUsers = Metrics.CreateGauge("github_copilot_total_active_chat_users", "Total number of active chat users.");
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
                    var client = _httpClientFactory.CreateClient();
                    client.DefaultRequestHeaders.Clear();
                    client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
                    client.DefaultRequestHeaders.Add("Authorization", $"token {_configuration["GitHub:Token"]}");
                    client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
                    client.DefaultRequestHeaders.Add("User-Agent", $"{_configuration["GitHub:Organization"]}-copilot-metrics-exporter");

                    var response = await client.GetAsync($"https://api.github.com/orgs/{_configuration["GitHub:Organization"]}/copilot/usage", stoppingToken);

                    if (response.IsSuccessStatusCode)
                    {
                        var metrics = await response.Content.ReadFromJsonAsync<List<CopilotUsageMetrics>>(stoppingToken);
                        if (metrics != null)
                        {
                            // Sort by day and select the most recent entry
                            var latestMetrics = metrics.OrderByDescending(m => m.Day).First();

                            _totalSuggestionsCount.Set(latestMetrics.TotalSuggestionsCount);
                            _totalAcceptancesCount.Set(latestMetrics.TotalAcceptancesCount);
                            _totalLinesSuggested.Set(latestMetrics.TotalLinesSuggested);
                            _totalLinesAccepted.Set(latestMetrics.TotalLinesAccepted);
                            _totalActiveUsers.Set(latestMetrics.TotalActiveUsers);
                            _totalChatAcceptances.Set(latestMetrics.TotalChatAcceptances);
                            _totalChatTurns.Set(latestMetrics.TotalChatTurns);
                            _totalActiveChatUsers.Set(latestMetrics.TotalActiveChatUsers);

                            foreach (var breakdown in latestMetrics.Breakdown)
                                {
                                    _suggestionsCountByLanguageEditor.WithLabels(breakdown.Language, breakdown.Editor).Set(breakdown.SuggestionsCount);
                                    _acceptancesCountByLanguageEditor.WithLabels(breakdown.Language, breakdown.Editor).Set(breakdown.AcceptancesCount);
                                    _linesSuggestedByLanguageEditor.WithLabels(breakdown.Language, breakdown.Editor).Set(breakdown.LinesSuggested);
                                    _linesAcceptedByLanguageEditor.WithLabels(breakdown.Language, breakdown.Editor).Set(breakdown.LinesAccepted);
                                    _activeUsersByLanguageEditor.WithLabels(breakdown.Language, breakdown.Editor).Set(breakdown.ActiveUsers);
                                }
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
        [JsonPropertyName("day")]
        public string Day { get; set; }

        [JsonPropertyName("total_suggestions_count")]
        public int TotalSuggestionsCount { get; set; }

        [JsonPropertyName("total_acceptances_count")]
        public int TotalAcceptancesCount { get; set; }

        [JsonPropertyName("total_lines_suggested")]
        public int TotalLinesSuggested { get; set; }

        [JsonPropertyName("total_lines_accepted")]
        public int TotalLinesAccepted { get; set; }

        [JsonPropertyName("total_active_users")]
        public int TotalActiveUsers { get; set; }

        [JsonPropertyName("total_chat_acceptances")]
        public int TotalChatAcceptances { get; set; }

        [JsonPropertyName("total_chat_turns")]
        public int TotalChatTurns { get; set; }

        [JsonPropertyName("total_active_chat_users")]
        public int TotalActiveChatUsers { get; set; }

        [JsonPropertyName("breakdown")]
        public List<Breakdown> Breakdown { get; set; }
    }

    public class Breakdown
    {
        [JsonPropertyName("language")]
        public string Language { get; set; }

        [JsonPropertyName("editor")]
        public string Editor { get; set; }

        [JsonPropertyName("suggestions_count")]
        public int SuggestionsCount { get; set; }

        [JsonPropertyName("acceptances_count")]
        public int AcceptancesCount { get; set; }

        [JsonPropertyName("lines_suggested")]
        public int LinesSuggested { get; set; }

        [JsonPropertyName("lines_accepted")]
        public int LinesAccepted { get; set; }

        [JsonPropertyName("active_users")]
        public int ActiveUsers { get; set; }
    }
}