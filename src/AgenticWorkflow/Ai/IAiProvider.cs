namespace AgenticWorkflow.Ai;

public interface IAiProvider
{
    Task<AiResponse> CompleteAsync(
        AiRequest request,
        CancellationToken cancellationToken);
}
