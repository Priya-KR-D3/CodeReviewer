using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CodeReviewer.Models;

[Route("api/review")]
[ApiController]
public class CodeReviewController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private const string OpenAiApiKey = "SAMPLE_SECRET";
    private const string OpenAiEndpoint = "https://api.openai.com/v1/chat/completions";

    public CodeReviewController(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    [HttpPost]
    public async Task<IActionResult> ReviewCode([FromBody] CodeReviewRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            return BadRequest(new { error = "Code input cannot be empty." });

        var prompt = $"""
            Analyze the following C# code for performance, security, readability, and best practices. 
            Suggest improvements and provide explanations.

            Code:
            ```csharp
            {request.Code}
            ```
            """;

        var openAiRequest = new
        {
            model = "gpt-4",
            messages = new[] { new { role = "system", content = prompt } },
            max_tokens = 1000
        };

        var requestBody = new StringContent(JsonSerializer.Serialize(openAiRequest), Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {OpenAiApiKey}");

        var response = await _httpClient.PostAsync(OpenAiEndpoint, requestBody);
        var responseContent = await response.Content.ReadAsStringAsync();

        return Ok(JsonSerializer.Deserialize<JsonElement>(responseContent));
    }
}