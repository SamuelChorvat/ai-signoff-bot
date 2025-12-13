using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AISignoffBot.Models;
using AISignoffBot.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace AISignoffBot.Services;

public class OpenAiClient : IAiClient
{
    private readonly HttpClient httpClient;
    private readonly AiOptions options;

    public OpenAiClient(HttpClient httpClient, IOptions<AiOptions> aiOptions)
    {
        this.httpClient = httpClient;
        options = aiOptions.Value;

        if (string.IsNullOrWhiteSpace(this.httpClient.BaseAddress?.ToString()))
        {
            this.httpClient.BaseAddress = new Uri("https://api.openai.com/");
        }
    }

    public async Task<string> GetVisionAnalysisAsync(
        string systemPrompt,
        string userPrompt,
        IReadOnlyList<EvidenceImage> evidenceImages,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException("AI ApiKey is not configured.");
        }

        if (!string.Equals(options.Provider, "OpenAI", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Unsupported AI provider: {options.Provider}");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.ApiKey);

        var payload = new OpenAiChatRequest
        {
            Model = options.Model,
            Messages =
            [
                new OpenAiMessage
                {
                    Role = "system",
                    Content = [new() { Type = "text", Text = systemPrompt }]
                },
                new OpenAiMessage
                {
                    Role = "user",
                    Content = BuildUserContent(userPrompt, evidenceImages)
                }
            ],
            ResponseFormat = new() { Type = "json_object" }
        };

        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(request, ct);
        var responseText = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            var reason = string.IsNullOrWhiteSpace(response.ReasonPhrase)
                ? "Unknown"
                : response.ReasonPhrase;

            var message = new StringBuilder()
                .Append($"OpenAI request failed {(int)response.StatusCode} ({reason})")
                .Append(!string.IsNullOrWhiteSpace(responseText) ? $": {responseText}" : string.Empty)
                .ToString();

            throw new HttpRequestException(message, null, response.StatusCode);
        }

        var result = JsonSerializer.Deserialize<OpenAiChatResponse>(responseText);
        var content = result?.Choices?.FirstOrDefault()?.Message?.Content;

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("AI response was empty.");
        }

        return content;
    }

    private static List<OpenAiMessageContent> BuildUserContent(string userPrompt, IReadOnlyList<EvidenceImage> evidenceImages)
    {
        var content = new List<OpenAiMessageContent>
        {
            new() { Type = "text", Text = userPrompt }
        };

        foreach (var image in evidenceImages.OrderBy(e => e.Index))
        {
            var base64 = Convert.ToBase64String(image.Bytes);
            var prefix = string.IsNullOrWhiteSpace(image.MimeType)
                ? "image/png"
                : image.MimeType;

            content.Add(new OpenAiMessageContent
            {
                Type = "image_url",
                ImageUrl = new OpenAiImageUrl { Url = $"data:{prefix};base64,{base64}" }
            });
        }

        return content;
    }

    private class OpenAiChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<OpenAiMessage> Messages { get; set; } = new();

        [JsonPropertyName("response_format")]
        public OpenAiResponseFormat ResponseFormat { get; set; } = new();
    }

    private class OpenAiResponseFormat
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "json_object";
    }

    private class OpenAiMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public List<OpenAiMessageContent> Content { get; set; } = new();
    }

    private class OpenAiMessageContent
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "text";

        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("image_url")]
        public OpenAiImageUrl? ImageUrl { get; set; }
    }

    private class OpenAiImageUrl
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;
    }

    private class OpenAiChatResponse
    {
        [JsonPropertyName("choices")]
        public List<OpenAiChatChoice>? Choices { get; set; }
    }

    private class OpenAiChatChoice
    {
        [JsonPropertyName("message")]
        public OpenAiResponseMessage? Message { get; set; }
    }

    private class OpenAiResponseMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}
