namespace AgenticSoftwareWorkflow.Conductor.Work;

/// <summary>What a proposal is: a specification, or the implementation of one.</summary>
public enum ProposalKind
{
    /// <summary>Merges by itself once every rule allows; maintainers revise it by reviewing.</summary>
    Specification,

    /// <summary>
    /// Merges by itself once the project's checks pass: the maintainer approved
    /// what to build (its specification), and the checks and the code reviewer
    /// judged how. One that changes what the rules reserve to maintainers still
    /// waits for their approval.
    /// </summary>
    Implementation,
}