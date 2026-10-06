using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgenticWorkflow.Ai;

public sealed class ClaudeCodeAiProvider(string executablePath = "claude.exe") : IAiProvider
{
    private readonly string _executablePath = executablePath;

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<AiResponse> CompleteAsync(
        AiRequest request,
        CancellationToken cancellationToken = default)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = this._executablePath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("--print");
        startInfo.ArgumentList.Add("--output-format");
        startInfo.ArgumentList.Add("json");
        startInfo.ArgumentList.Add("--no-session-persistence");

        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            startInfo.ArgumentList.Add("--system-prompt");
            startInfo.ArgumentList.Add(request.SystemPrompt);
        }

        using Process process = new()
        {
            StartInfo = startInfo
        };

        if (!process.Start())
        {
            throw new InvalidOperationException(
                $"Unable to start Claude Code executable '{this._executablePath}'.");
        }

        await process.StandardInput.WriteAsync(request.UserPrompt);
        await process.StandardInput.WriteLineAsync();
        process.StandardInput.Close();

        Task<string> standardOutputTask =
            process.StandardOutput.ReadToEndAsync(cancellationToken);

        Task<string> standardErrorTask =
            process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        string standardOutput = await standardOutputTask;
        string standardError = await standardErrorTask;

        if (process.ExitCode != 0)
        {
            string message = string.IsNullOrWhiteSpace(standardError)
                ? $"Claude Code exited with code {process.ExitCode}."
                : $"Claude Code exited with code {process.ExitCode}:\n{standardError.Trim()}";

            throw new InvalidOperationException(message);
        }

        return ParseResponse(standardOutput);
    }

    private static AiResponse ParseResponse(string json)
    {
        ClaudeCodeResponse response =
            JsonSerializer.Deserialize<ClaudeCodeResponse>(
                json,
                JsonSerializerOptions)
            ?? throw new InvalidOperationException(
                "Claude Code returned an empty or invalid JSON response.");

        if (response.IsError)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(response.Result)
                    ? "Claude Code reported an error."
                    : response.Result);
        }

        string model = GetModel(response);

        int inputTokens = response.Usage?.InputTokens ?? 0;
        int outputTokens = response.Usage?.OutputTokens ?? 0;

        return new AiResponse(
            response.Result ?? string.Empty,
            model,
            inputTokens,
            outputTokens);
    }

    private static string GetModel(ClaudeCodeResponse response)
        => response.ModelUsage is null || response.ModelUsage.Count == 0
            ? "unknown"
            : response.ModelUsage.Count == 1
                ? response.ModelUsage.Keys.First()
                : string.Join(
                        ", ",
                        response.ModelUsage.Keys.OrderBy(model => model)
                    );

    private sealed class ClaudeCodeResponse
    {
        [JsonPropertyName("result")]
        public string? Result { get; set; }

        [JsonPropertyName("is_error")]
        public bool IsError { get; set; }

        [JsonPropertyName("usage")]
        public ClaudeUsage? Usage { get; set; }

        [JsonPropertyName("modelUsage")]
        public Dictionary<string, ClaudeModelUsage>? ModelUsage { get; set; }
    }

    private sealed class ClaudeUsage
    {
        [JsonPropertyName("input_tokens")]
        public int InputTokens { get; set; }

        [JsonPropertyName("output_tokens")]
        public int OutputTokens { get; set; }
    }

    private sealed class ClaudeModelUsage
    {
        [JsonPropertyName("inputTokens")]
        public int InputTokens { get; set; }

        [JsonPropertyName("outputTokens")]
        public int OutputTokens { get; set; }
    }
}
