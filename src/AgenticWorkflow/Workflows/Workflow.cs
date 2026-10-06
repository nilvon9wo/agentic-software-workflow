namespace AgenticWorkflow.Workflows;

public sealed class Workflow(
    string name,
    IReadOnlyList<IWorkflowStep> steps,
    IWorkflowStateStore stateStore,
    WorkflowRetryPolicy? retryPolicy = null)
{
    private readonly IReadOnlyList<IWorkflowStep> _steps = steps;
    private readonly IWorkflowStateStore _stateStore = stateStore;
    private readonly WorkflowRetryPolicy? _retryPolicy = retryPolicy;

    public string Name { get; } = name;

    public async Task<WorkflowResult> ExecuteAsync(
        WorkflowContext context,
        CancellationToken cancellationToken)
    {
        WorkflowExecutionState state =
            await this._stateStore.LoadAsync(
                this.Name,
                cancellationToken)
            ?? new WorkflowExecutionState
            {
                WorkflowName = this.Name,
                Status = WorkflowStatus.Failed
            };

        if (state.WorkflowName.Length == 0)
        {
            state.WorkflowName = this.Name;
        }

        if (state.Status == WorkflowStatus.Succeeded)
        {
            return new WorkflowResult(
                WorkflowStatus.Succeeded,
                []);
        }

        Console.WriteLine(
            $"Workflow '{this.Name}' started.");

        List<StepExecution> executions = [];

        int resumeIndex = this.GetResumeIndex(state);

        for (int index = 0; index < this._steps.Count;)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IWorkflowStep step = this._steps[index];

            if (index < resumeIndex &&
                state.Steps.TryGetValue(
                    step.Name,
                    out StepState? existingState) &&
                existingState.Status == StepStatus.Succeeded)
            {
                Console.WriteLine(
                    $"[{DateTimeOffset.Now:HH:mm:ss}] " +
                    $"SKIP {step.Name} (already completed)");

                executions.Add(
                    new StepExecution(
                        step.Name,
                        StepResult.Success(
                            existingState.Message)));

                index++;
                continue;
            }

            Console.WriteLine(
                $"[{DateTimeOffset.Now:HH:mm:ss}] START {step.Name}");

            DateTimeOffset startTime =
                DateTimeOffset.UtcNow;

            StepResult result;

            try
            {
                result = await step.ExecuteAsync(
                    context,
                    cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                TimeSpan elapsed =
                    DateTimeOffset.UtcNow - startTime;

                Console.WriteLine(
                    $"[{DateTimeOffset.Now:HH:mm:ss}] " +
                    $"CANCELLED {step.Name} " +
                    $"({elapsed.TotalSeconds:F1}s)");

                throw;
            }
            catch (Exception exception)
            {
                TimeSpan elapsed =
                    DateTimeOffset.UtcNow - startTime;

                Console.WriteLine(
                    $"[{DateTimeOffset.Now:HH:mm:ss}] " +
                    $"ERROR {step.Name} " +
                    $"({elapsed.TotalSeconds:F1}s): {exception}");

                result = StepResult.Failure(
                    exception.Message);
            }

            TimeSpan duration =
                DateTimeOffset.UtcNow - startTime;

            Console.WriteLine(
                $"[{DateTimeOffset.Now:HH:mm:ss}] " +
                $"{GetLogStatus(result)} {step.Name} " +
                $"({duration.TotalSeconds:F1}s): " +
                $"{result.Message}");

            executions.Add(
                new StepExecution(
                    step.Name,
                    result));

            state.Steps[step.Name] = new StepState
            {
                Status = result.Status,
                Message = result.Message,
                StartedAt = startTime,
                CompletedAt = DateTimeOffset.UtcNow
            };

            if (result.Waiting)
            {
                state.Status = WorkflowStatus.Waiting;
                state.WaitingStep = step.Name;
                state.WaitingReason = result.Message;
                state.ResumeFromStep = result.ResumeFromStep;

                await this._stateStore.SaveAsync(
                    state,
                    cancellationToken);

                Console.WriteLine(
                    $"Workflow '{this.Name}' is waiting " +
                    $"at step '{step.Name}'.");

                return new WorkflowResult(
                    WorkflowStatus.Waiting,
                    executions);
            }

            if (result.Retry)
            {
                await this.SaveRetryAsync(
                    state,
                    step.Name,
                    result,
                    cancellationToken);

                resumeIndex = this.GetStepIndex(
                    result.ResumeFromStep!);

                index = resumeIndex;

                continue;
            }

            if (result.Failed &&
                this.TryGetRepairStep(
                    step.Name,
                    state,
                    out string? repairStepName))
            {
                state.Status = WorkflowStatus.Succeeded;
                state.WaitingStep = null;
                state.WaitingReason = null;
                state.ResumeFromStep = repairStepName;

                await this._stateStore.SaveAsync(
                    state,
                    cancellationToken);

                int retryCount =
                    state.RetryCounts.TryGetValue(
                        repairStepName!,
                        out int existingRetryCount)
                            ? existingRetryCount + 1
                            : 1;

                state.RetryCounts[repairStepName!] = retryCount;

                await this._stateStore.SaveAsync(
                    state,
                    cancellationToken);

                Console.WriteLine(
                    $"Workflow '{this.Name}' scheduling " +
                    $"repair step '{repairStepName}' " +
                    $"(attempt {retryCount}).");

                resumeIndex = this.GetStepIndex(
                    repairStepName!);

                index = resumeIndex;

                continue;
            }

            if (result.Failed)
            {
                state.Status = WorkflowStatus.Failed;
                state.WaitingStep = null;
                state.WaitingReason = null;
                state.ResumeFromStep = null;

                await this._stateStore.SaveAsync(
                    state,
                    cancellationToken);

                Console.WriteLine(
                    $"Workflow '{this.Name}' failed " +
                    $"at step '{step.Name}'.");

                return new WorkflowResult(
                    WorkflowStatus.Failed,
                    executions);
            }

            state.Status = WorkflowStatus.Succeeded;
            state.WaitingStep = null;
            state.WaitingReason = null;

            if (state.ResumeFromStep is not null &&
                step.Name.Equals(
                    state.ResumeFromStep,
                    StringComparison.OrdinalIgnoreCase))
            {
                state.ResumeFromStep = null;
            }

            await this._stateStore.SaveAsync(
                state,
                cancellationToken);

            index++;
        }

        state.Status = WorkflowStatus.Succeeded;
        state.WaitingStep = null;
        state.WaitingReason = null;
        state.ResumeFromStep = null;

        await this._stateStore.SaveAsync(
            state,
            cancellationToken);

        Console.WriteLine(
            $"Workflow '{this.Name}' completed successfully.");

        return new WorkflowResult(
            WorkflowStatus.Succeeded,
            executions);
    }

    private bool TryGetRepairStep(
        string failedStep,
        WorkflowExecutionState state,
        out string? repairStep)
    {
        repairStep = null;

        if (this._retryPolicy is null)
        {
            return false;
        }

        if (!this._retryPolicy.TryGetRepairStep(
                failedStep,
                out repairStep))
        {
            return false;
        }

        int attempt =
            state.RetryCounts.TryGetValue(
                repairStep!,
                out int existingAttempt)
                    ? existingAttempt
                    : 0;

        return attempt < this._retryPolicy.MaximumAttempts;
    }

    private async Task SaveRetryAsync(
        WorkflowExecutionState state,
        string stepName,
        StepResult result,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(
                result.ResumeFromStep))
        {
            throw new InvalidOperationException(
                $"Step '{stepName}' requested a retry without " +
                "specifying a resume step.");
        }

        string retryStep =
            result.ResumeFromStep;

        int retryCount =
            state.RetryCounts.TryGetValue(
                retryStep,
                out int existingRetryCount)
                    ? existingRetryCount + 1
                    : 1;

        state.RetryCounts[retryStep] = retryCount;
        state.Status = WorkflowStatus.Succeeded;
        state.WaitingStep = null;
        state.WaitingReason = null;
        state.ResumeFromStep = retryStep;

        await this._stateStore.SaveAsync(
            state,
            cancellationToken);

        Console.WriteLine(
            $"Workflow '{this.Name}' retrying from " +
            $"step '{retryStep}' " +
            $"(attempt {retryCount}).");
    }

    private int GetResumeIndex(
        WorkflowExecutionState state) => string.IsNullOrWhiteSpace(
                state.ResumeFromStep)
            ? this._steps.Count
            : this.GetStepIndex(
            state.ResumeFromStep);

    private int GetStepIndex(string stepName)
    {
        for (int index = 0; index < this._steps.Count; index++)
        {
            if (this._steps[index].Name.Equals(
                    stepName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        throw new InvalidOperationException(
            $"Workflow state requests resume from unknown step " +
            $"'{stepName}'.");
    }

    private static string GetLogStatus(
        StepResult result)
        => result.Status switch
        {
            StepStatus.Succeeded => "END",
            StepStatus.Waiting => "WAITING",
            StepStatus.Retry => "RETRY",
            StepStatus.Failed => "FAILED",
            _ => "UNKNOWN"
        };
}