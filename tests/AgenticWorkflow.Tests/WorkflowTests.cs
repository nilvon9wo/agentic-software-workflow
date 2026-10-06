using AgenticWorkflow.Artifacts;
using AgenticWorkflow.Workflows;
using Xunit;

namespace AgenticWorkflow.Tests;

public sealed class WorkflowTests
{
    [Fact]
    public async Task Workflow_stops_after_failed_step()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            Guid.NewGuid().ToString("N"));

        IArtifactStore artifactStore =
            new FileArtifactStore(root);

        IWorkflowStateStore stateStore =
            new FileWorkflowStateStore(
                Path.Combine(root, "workflow-state"));

        WorkflowContext context =
            new(artifactStore, root);

        Workflow workflow = new(
            "test",
            [
                new TestStep("first", true),
                new TestStep("second", false),
                new TestStep("third", true)
            ],
            stateStore);

        using CancellationTokenSource cancellationTokenSource =
            new(TimeSpan.FromMinutes(5));

        WorkflowResult result =
            await workflow.ExecuteAsync(
                context,
                cancellationTokenSource.Token);

        Assert.True(result.Failed);
        Assert.False(result.Succeeded);
        Assert.False(result.Waiting);

        Assert.Equal(2, result.Steps.Count);
        Assert.Equal("second", result.Steps[1].StepName);
    }

    private sealed class TestStep(
        string name,
        bool succeeds) : IWorkflowStep
    {
        private readonly bool _succeeds = succeeds;

        public string Name { get; } = name;

        public Task<StepResult> ExecuteAsync(
            WorkflowContext context,
            CancellationToken cancellationToken)
            => Task.FromResult(
                this._succeeds
                    ? StepResult.Success("ok")
                    : StepResult.Failure("failed"));
    }

    [Fact]
    public async Task Workflow_waits_and_persists_state_after_waiting_step()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            Guid.NewGuid().ToString("N"));

        IArtifactStore artifactStore =
            new FileArtifactStore(root);

        FileWorkflowStateStore stateStore =
            new(
                Path.Combine(root, "workflow-state"));

        WorkflowContext context =
            new(artifactStore, root);

        Workflow workflow = new(
            "waiting-test",
            [
                new TestStep("first", true),
            new WaitingTestStep(),
            new TestStep("third", true)
            ],
            stateStore);

        using CancellationTokenSource cancellationTokenSource =
            new(TimeSpan.FromMinutes(5));

        WorkflowResult result =
            await workflow.ExecuteAsync(
                context,
                cancellationTokenSource.Token);

        Assert.True(result.Waiting);
        Assert.False(result.Succeeded);
        Assert.False(result.Failed);

        Assert.Equal(2, result.Steps.Count);
        Assert.Equal("waiting", result.Steps[1].StepName);

        WorkflowExecutionState? state =
            await stateStore.LoadAsync(
                "waiting-test",
                cancellationTokenSource.Token);

        Assert.NotNull(state);
        Assert.Equal(
            WorkflowStatus.Waiting,
            state.Status);

        Assert.Equal(
            "waiting",
            state.WaitingStep);

        Assert.Equal(
            StepStatus.Succeeded,
            state.Steps["first"].Status);

        Assert.Equal(
            StepStatus.Waiting,
            state.Steps["waiting"].Status);

        Assert.False(
            state.Steps.ContainsKey("third"));
    }

    private sealed class WaitingTestStep : IWorkflowStep
    {
        public string Name => "waiting";

        public Task<StepResult> ExecuteAsync(
            WorkflowContext context,
            CancellationToken cancellationToken)
            => Task.FromResult(
                StepResult.Wait("Human input is required."));
    }

    [Fact]
    public async Task Workflow_resumes_from_requested_step_after_waiting()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            Guid.NewGuid().ToString("N"));

        IArtifactStore artifactStore =
            new FileArtifactStore(root);

        FileWorkflowStateStore stateStore =
            new(
                Path.Combine(root, "workflow-state"));

        WorkflowContext context =
            new(artifactStore, root);

        Workflow workflow = new(
            "loop-test",
            [
                new CountingTestStep("first"),
            new WaitingThenSuccessStep(),
            new CountingTestStep("third")
            ],
            stateStore);

        using CancellationTokenSource cancellationTokenSource =
            new(TimeSpan.FromMinutes(5));

        WorkflowResult firstResult =
            await workflow.ExecuteAsync(
                context,
                cancellationTokenSource.Token);

        Assert.True(firstResult.Waiting);

        WorkflowExecutionState? waitingState =
            await stateStore.LoadAsync(
                "loop-test",
                cancellationTokenSource.Token);

        Assert.NotNull(waitingState);

        Assert.Equal(
            "waiting",
            waitingState.WaitingStep);

        Assert.Equal(
            "waiting",
            waitingState.ResumeFromStep);

        WorkflowResult secondResult =
            await workflow.ExecuteAsync(
                context,
                cancellationTokenSource.Token);

        Assert.True(secondResult.Succeeded);

        Assert.Contains(
            secondResult.Steps,
            static execution =>
                execution.StepName == "waiting");

        Assert.Contains(
            secondResult.Steps,
            static execution =>
                execution.StepName == "third");
    }

    private sealed class CountingTestStep(
        string name) : IWorkflowStep
    {
        public string Name { get; } = name;

        public Task<StepResult> ExecuteAsync(
            WorkflowContext context,
            CancellationToken cancellationToken)
            => Task.FromResult(
                StepResult.Success("ok"));
    }

    private sealed class WaitingThenSuccessStep : IWorkflowStep
    {
        private bool _hasWaited;

        public string Name => "waiting";

        public Task<StepResult> ExecuteAsync(
            WorkflowContext context,
            CancellationToken cancellationToken)
        {
            if (!this._hasWaited)
            {
                this._hasWaited = true;

                return Task.FromResult(
                    StepResult.Wait(
                        "Human input is required.",
                        "waiting"));
            }

            return Task.FromResult(
                StepResult.Success("resumed"));
        }
    }
}