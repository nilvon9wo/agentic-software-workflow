using AgenticSoftwareWorkflow.Conductor.Git;
using AgenticSoftwareWorkflow.Conductor.Work;
using AgenticSoftwareWorkflow.Conductor.Workflow.Build;

namespace AgenticSoftwareWorkflow.Conductor.Test.Workflow.Build;

public sealed class BuildBriefTest
{
    private static readonly WorkItem Item = new(
        new WorkItemId("github:owner/repository", "7"),
        "Add a clock",
        "Show the time.",
        [],
        [],
        false
    );

    private static readonly BuildJob Job = new(Item, "# Show the time\n", new Workspace("/w", "aswf/build-7"));

    [Fact]
    public void Describe_WhenThereIsNothingToActOn_ShowsTheItemAndItsSpecificationOnly()
    {
        // Arrange
        // Nothing to arrange: the job is a shared constant.

        // Act
        string brief = BuildBrief.Describe(Job, "What to fix", "");

        // Assert
        Assert.Equal(
            "# Add a clock\n\nShow the time.\n\n## The approved specification\n\n````markdown\n# Show the time\n````\n",
            brief
        );
    }

    [Fact]
    public void Describe_WhenThereAreFindings_AddsThemUnderTheirHeading()
    {
        // Arrange
        // Nothing to arrange: the job is a shared constant.

        // Act
        string brief = BuildBrief.Describe(Job, "What to fix", "- AC-2 has no test.\n");

        // Assert
        Assert.EndsWith("````\n\n## What to fix\n\n- AC-2 has no test.\n", brief, StringComparison.Ordinal);
    }
}