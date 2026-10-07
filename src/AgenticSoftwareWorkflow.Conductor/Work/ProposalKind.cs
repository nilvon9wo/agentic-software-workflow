namespace AgenticSoftwareWorkflow.Conductor.Work;

/// <summary>What a proposal is: a specification, or the implementation of one.</summary>
public enum ProposalKind
{
    /// <summary>Merges by itself once every rule allows; maintainers revise it by reviewing.</summary>
    Specification,

    /// <summary>
    /// Waits for a maintainer to merge it: until the build stage has earned that
    /// trust, an implementation is merged by a person, never by itself.
    /// </summary>
    Implementation,
}