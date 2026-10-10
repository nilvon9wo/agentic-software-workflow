using AgenticSoftwareWorkflow.Conductor.Functional;

namespace AgenticSoftwareWorkflow.Conductor.Work;

/// <summary>The work source answered, but its response could not be read.</summary>
public sealed record WorkResponseMalformed : ExpectedFailure
{
    public WorkResponseMalformed(string detail)
        : base($"The work source's response could not be read: {detail}")
    {
    }
}