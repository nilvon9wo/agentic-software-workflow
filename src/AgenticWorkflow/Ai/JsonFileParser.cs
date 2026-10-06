using System.Text.Json;

namespace AgenticWorkflow.Ai;

public static class JsonFileParser
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static T Parse<T>(string response)
    {
        string json = ExtractJson(response);
        T? result = JsonSerializer.Deserialize<T>(json, JsonSerializerOptions);

        return result ?? throw new InvalidOperationException("AI returned an empty JSON object.");
    }

    private static string ExtractJson(string response)
    {
        string trimmed = response.Trim();

        if (trimmed.StartsWith("```") && trimmed.EndsWith("```"))
        {
            int firstNewline = trimmed.IndexOf('\n');
            trimmed = firstNewline >= 0
                ? trimmed[(firstNewline + 1)..].TrimEnd('`', '\r', '\n', ' ')
                : trimmed.Trim('`');
        }

        int objectStart = trimmed.IndexOf('{');
        int objectEnd = trimmed.LastIndexOf('}');

        return objectStart >= 0 && objectEnd > objectStart
            ? trimmed[objectStart..(objectEnd + 1)]
            : throw new InvalidOperationException("AI response did not contain a JSON object.");
    }
}
