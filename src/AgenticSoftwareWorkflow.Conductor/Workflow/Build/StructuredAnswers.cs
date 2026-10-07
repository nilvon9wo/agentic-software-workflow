using System.Text.Json;
using AgenticSoftwareWorkflow.Conductor.Agents;
using LanguageExt;

namespace AgenticSoftwareWorkflow.Conductor.Workflow.Build;

/// <summary>Reads a worker's structured answer, or explains why it cannot be used.</summary>
internal static class StructuredAnswers
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Fin<T> Read<T>(AgentResult result)
        where T : class, IStructuredAnswer =>
        result.StructuredOutputJson.Match(
            Some: Parse<T>,
            None: () => Fin.Fail<T>(new WorkerAnswerUnusable("there was no structured output"))
        );

    private static Fin<T> Parse<T>(string json)
        where T : class
    {
        Try<T?> deserialize = Try.lift(() => JsonSerializer.Deserialize<T>(json, JsonOptions));
        return deserialize
            .ToFin()
            .MapFail(error => new WorkerAnswerUnusable(error.Message))
            .Bind(
                answer => answer is null
                    ? Fin.Fail<T>(new WorkerAnswerUnusable("the answer was the JSON literal null"))
                    : Fin.Succ(answer)
            );
    }
}