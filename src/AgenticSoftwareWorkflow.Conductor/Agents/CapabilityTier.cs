namespace AgenticSoftwareWorkflow.Conductor.Agents;

/// <summary>
/// How capable a model a role needs, independent of any vendor. Each
/// <see cref="IAgentic"/> maps tiers to its own models, so a role never
/// names a model and swapping providers never touches the workflow.
/// </summary>
public enum CapabilityTier
{
    /// <summary>Summarising, triage, classification: cheap and fast.</summary>
    Small,

    /// <summary>Most specification, test, and implementation work.</summary>
    Standard,

    /// <summary>The hardest judgement: arbitration between conflicting artifacts.</summary>
    Strongest,
}