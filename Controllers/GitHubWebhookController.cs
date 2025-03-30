using CodeReviewer.Models;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

[Route("api/github-webhook")]
[ApiController]
public class GitHubWebhookController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private const string GitHubToken = "SAMPLE_SECRET";
    private const string OpenAiApiKey = "YOUR_OPENAI_API_KEY";
    private const string OpenAiEndpoint = "https://api.openai.com/v1/chat/completions";

    public GitHubWebhookController(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    [HttpPost]
    public async Task<IActionResult> HandleGitHubWebhook([FromBody] JsonElement payload)
    {
        string action = payload.GetProperty("action").GetString();
        if (action != "opened" && action != "synchronize") return Ok();

        var pullRequest = payload.GetProperty("pull_request");
        string prUrl = pullRequest.GetProperty("html_url").GetString();
        string repoUrl = payload.GetProperty("repository").GetProperty("url").GetString();
        string prNumber = pullRequest.GetProperty("number").ToString();

        // Get changed files
        var files = await GetChangedFiles(repoUrl, prNumber);
        if (files.Count == 0) return Ok("No changed files.");

        // Analyze files with GPT-4
        foreach (var file in files)
        {
            string reviewComment = await ReviewCode(file.Content);
            if (!string.IsNullOrEmpty(reviewComment))
            {
                await PostGitHubComment(repoUrl, prNumber, file.Filename, reviewComment);
            }
        }

        return Ok();
    }

    private async Task<List<GitHubFile>> GetChangedFiles(string repoUrl, string prNumber)
    {
        string apiUrl = $"{repoUrl}/pulls/{prNumber}/files";
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {GitHubToken}");

        var response = await _httpClient.GetAsync(apiUrl);
        var jsonResponse = await response.Content.ReadAsStringAsync();
        var files = JsonSerializer.Deserialize<List<GitHubFile>>(jsonResponse);

        foreach (var file in files)
        {
            file.Content = await GetFileContent(repoUrl, file.Filename);
        }

        return files;
    }

    private async Task<string> GetFileContent(string repoUrl, string filePath)
    {
        string apiUrl = $"{repoUrl}/contents/{filePath}";
        var response = await _httpClient.GetAsync(apiUrl);
        var jsonResponse = await response.Content.ReadAsStringAsync();
        var json = JsonSerializer.Deserialize<JsonElement>(jsonResponse);
        return json.GetProperty("content").GetString();
    }

    private async Task<string> ReviewCode(string code)
    {
        var prompt = $"""
            Analyze the following C# code from a GitHub PR and provide a review:
            - Identify performance issues.
            - Check for security vulnerabilities.
            - Suggest improvements based on best practices.
            - Explain any suggested changes clearly.
            
            Code:
            ```csharp
            {code}
            ```
            """;

        var requestBody = new StringContent(JsonSerializer.Serialize(new
        {
            model = "gpt-4",
            messages = new[] { new { role = "system", content = prompt } },
            max_tokens = 1000
        }), Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {OpenAiApiKey}");

        var response = await _httpClient.PostAsync(OpenAiEndpoint, requestBody);
        var responseContent = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(responseContent).GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
    }

    private async Task PostGitHubComment(string repoUrl, string prNumber, string filename, string reviewComment)
    {
        var commentBody = new
        {
            body = $"### Code Review for `{filename}`\n\n{reviewComment}"
        };

        string apiUrl = $"{repoUrl}/issues/{prNumber}/comments";
        var requestBody = new StringContent(JsonSerializer.Serialize(commentBody), Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {GitHubToken}");
        await _httpClient.PostAsync(apiUrl, requestBody);
    }
}
