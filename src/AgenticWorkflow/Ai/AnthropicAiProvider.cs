using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AgenticWorkflow.Ai;

public sealed class AnthropicAiProvider(
    HttpClient httpClient,
    string apiKey,
    string model) : IAiProvider
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly string _apiKey = apiKey;
    private readonly string _model = model;

    public async Task<AiResponse> CompleteAsync(
        AiRequest request,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage message = new(
            HttpMethod.Post,
            "https://api.anthropic.com/v1/messages");

        message.Headers.Add("x-api-key", this._apiKey);
        message.Headers.Add("anthropic-version", "2023-06-01");
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var body = new
        {
            model = this._model,
            max_tokens = request.MaxTokens,
            system = request.SystemPrompt,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = request.UserPrompt
                }
            }
        };

        string json = JsonSerializer.Serialize(body);
        message.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await this._httpClient.SendAsync(message, cancellationToken);
        string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Anthropic API returned {(int)response.StatusCode}: {responseBody}");
        }

        using JsonDocument document = JsonDocument.Parse(responseBody);
        JsonElement root = document.RootElement;
        string text = root
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString()
            ?? throw new InvalidOperationException("Anthropic response contained no text.");

        int inputTokens = root.TryGetProperty("usage", out JsonElement usage)
            && usage.TryGetProperty("input_tokens", out JsonElement input)
            ? input.GetInt32()
            : 0;

        int outputTokens = usage.ValueKind != JsonValueKind.Undefined
            && usage.TryGetProperty("output_tokens", out JsonElement output)
            ? output.GetInt32()
            : 0;

        return new AiResponse(text, this._model, inputTokens, outputTokens);
    }
}
