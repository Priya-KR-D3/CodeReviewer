using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using CodeReviewer.Models;

namespace CodeReviewer.Controllers
{
    public class CodeReviewController : Controller
    {
        private readonly HttpClient _httpClient;
        private const string OpenAiApiKey = "YOUR_OPENAI_API_KEY";
        private const string OpenAiEndpoint = "https://api.openai.com/v1/chat/completions";

        public CodeReviewController(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }
        public async Task<IActionResult> ReviewCode([FromBody] CodeReviewRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Code))
                return BadRequest(new { error = "Code input cannot be empty." });

            // Construct the prompt
            var prompt = $"""
            Analyze the following C# code and provide a detailed review. 
            - Identify performance issues.
            - Check for security vulnerabilities.
            - Suggest improvements for readability and maintainability.
            - Follow SOLID principles and best coding practices.
            - Provide better alternative implementations if applicable.
            - Explain all suggestions clearly.
            
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
}
