using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CodeReviewer.Models;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

[ApiController]
[Route("api/[controller]")]
public class CodeReviewController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private const string GeminiApiKey = "AIzaSyBbEoTyKoWZSRQyogioyfCxIfGSNm-at-o";
    private const string GeminiEndpoint = "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions";

    public CodeReviewController(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    [HttpPost("review")]
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

        var geminiRequest = new
        {
            model = "gemini-2.0-flash", // Use gpt-3.5-turbo for free/cheaper access
            messages = new[] { new { role = "user", content = prompt } }, // Corrected role
            max_tokens = 1000,
            stream = true
        };

        var content = new StringContent(JsonConvert.SerializeObject(geminiRequest), Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {GeminiApiKey}");


        HttpResponseMessage response = await _httpClient.PostAsync(GeminiEndpoint, content); // important.
        response.EnsureSuccessStatusCode();

        using (var stream = await response.Content.ReadAsStreamAsync())
        using (var reader = new System.IO.StreamReader(stream))
        {
            string fullResponse = "";
            while (!reader.EndOfStream)
            {
                string line = await reader.ReadLineAsync();
                if (line.StartsWith("data: "))
                {
                    string jsonString = line.Substring(6); // Remove "data: "

                    if (jsonString == "[DONE]")
                    {
                        break;
                    }

                    JsonDocument jsonDocument = JsonDocument.Parse(jsonString);
                    if (jsonDocument.RootElement.TryGetProperty("choices", out JsonElement choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0)
                    {
                        if (choices[0].TryGetProperty("delta", out JsonElement delta) && delta.TryGetProperty("content", out JsonElement contentElement))
                        {
                            fullResponse += contentElement.GetString();
                        }
                    }
                }
            }
            return Ok(fullResponse);
        }
    }
}