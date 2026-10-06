namespace AgenticSoftwareWorkflow.Conductor.Agents;

/// <summary>
/// What a run consumed. <see cref="ListPriceUsd"/> is what the run would cost at
/// pay-per-token prices; on a subscription it is drawn from the plan's usage
/// allowance instead, which makes it the honest measure of how much of that
/// allowance a role spends.
/// </summary>
public sealed record AgentUsage(
    int InputTokens,
    int OutputTokens,
    decimal ListPriceUsd,
    int Turns,
    IReadOnlyList<string> Models
);