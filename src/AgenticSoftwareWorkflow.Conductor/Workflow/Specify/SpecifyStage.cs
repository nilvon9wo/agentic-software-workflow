using System.Text;
using System.Text.Json;
using AgenticSoftwareWorkflow.Conductor.Agents;
using AgenticSoftwareWorkflow.Conductor.Functional;
using AgenticSoftwareWorkflow.Conductor.Gates;
using AgenticSoftwareWorkflow.Conductor.Work;
using LanguageExt;

namespace AgenticSoftwareWorkflow.Conductor.Workflow.Specify;

/// <summary>
/// The first pipeline stage: turns a ready work item into a specification, or
/// — when it is genuinely unclear — into questions for a maintainer.
/// </summary>
/// <remarks>
/// The specifier only reads and answers; this stage, not the AI, writes the
/// specification file. Deterministic code does what deterministic code can,
/// and the role keeps the least authority its job needs. The written file is
/// held to the project's own checks before anything is proposed: a failure
/// gets one repair, and a second failure is reported, never proposed.
/// </remarks>
public sealed class SpecifyStage(IWorkSupplying work, IAgentic agent, IGateKeeping gate)
{
    // GitHub content is the same whichever OS the conductor runs on.
    private const char LineBreak = '\n';

    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(15);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IWorkSupplying _work = work;
    private readonly IAgentic _agent = agent;
    private readonly IGateKeeping _gate = gate;

    public async Task<Fin<SpecifyOutcome>> Run(
        WorkItemId id,
        string workingDirectory,
        CancellationToken cancellationToken
    )
    {
        Fin<WorkItem> item = await this._work.Read(id, cancellationToken);
        return await item.Then(
            read => this.Begin(read, () => this.Specify(read, workingDirectory, cancellationToken), cancellationToken)
        );
    }

    /// <summary>
    /// Revises a proposed specification in response to its review, in a workspace
    /// holding the proposal's branch. The revision is held to the same checks, and
    /// waits, like specifying does, while a question the specifier asked is unanswered.
    /// </summary>
    public async Task<Fin<SpecifyOutcome>> Revise(
        ProposalReview review,
        string workingDirectory,
        CancellationToken cancellationToken
    )
    {
        Fin<WorkItem> item = await this._work.Read(review.Item, cancellationToken);
        return await item.Then(
            read => this.Begin(
                read,
                () => this.Attempt(RevisionAttempt(read, review, workingDirectory), cancellationToken),
                cancellationToken
            )
        );
    }

    private static SpecifyAttempt RevisionAttempt(WorkItem item, ProposalReview review, string workingDirectory)
    {
        string fileName = SpecificationFileName.For(item);
        string path = Path.Combine(workingDirectory, WorkspaceLayout.SpecificationDirectory, fileName);
        string current = File.Exists(path)
            ? File.ReadAllText(path)
            : "(The specification file was not found; write it afresh.)";
        string brief = RevisionBrief.Describe(item, current, review.Feedback);
        return new SpecifyAttempt(item, workingDirectory, brief, true);
    }

    private static AgentTask SpecifierTask(SpecifyAttempt attempt) =>
        new AgentTask(WorkflowRoles.Specifier, attempt.Brief, attempt.WorkingDirectory, Timeout)
            .WithInstructions(RoleInstructions.Specifier)
            .WithOutputSchema(SpecifierAnswer.Schema);

    private static Fin<SpecifierAnswer> ReadAnswer(AgentResult result) =>
        result.StructuredOutputJson.Match(
            Some: Parse,
            None: () => Fin.Fail<SpecifierAnswer>(new SpecifierAnswerUnusable("there was no structured output"))
        );

    private static Fin<SpecifierAnswer> Parse(string json)
    {
        Try<SpecifierAnswer?> deserialize = Try.lift(
            () => JsonSerializer.Deserialize<SpecifierAnswer>(json, JsonOptions)
        );
        return deserialize
            .ToFin()
            .MapFail(error => new SpecifierAnswerUnusable(error.Message))
            .Bind(RequirePresent);
    }

    private static Fin<SpecifierAnswer> RequirePresent(SpecifierAnswer? answer) =>
        answer is null
            ? Fin.Fail<SpecifierAnswer>(new SpecifierAnswerUnusable("the answer was the JSON literal null"))
            : Fin.Succ(answer);

    private static string DescribeQuestions(IReadOnlyList<string> questions)
    {
        StringBuilder comment = new();
        _ = comment.Append("**Questions before this can be specified**").Append(LineBreak);
        _ = comment.Append(LineBreak);
        foreach ((string question, int index) in questions.Select((question, index) => (question, index)))
        {
            _ = comment.Append($"{index + 1}. {question}").Append(LineBreak);
        }

        _ = comment.Append(LineBreak);
        _ = comment.Append("Please reply in this thread. Only maintainers' replies are read.").Append(LineBreak);
        return comment.ToString();
    }

    private static async Task<Specified> WriteSpecification(
        SpecifyAttempt attempt,
        string specification,
        CancellationToken cancellationToken
    )
    {
        string fileName = SpecificationFileName.For(attempt.Item);
        string relativePath = $"{WorkspaceLayout.SpecificationDirectory}/{fileName}";
        string fullPath = Path.Combine(attempt.WorkingDirectory, WorkspaceLayout.SpecificationDirectory, fileName);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(fullPath, specification, cancellationToken);
        return new Specified(relativePath);
    }

    // An answered item stops waiting before the specifier reads the answers;
    // an unanswered one is left alone, so no agent run is spent re-asking.
    private Task<Fin<SpecifyOutcome>> Begin(
        WorkItem item,
        Func<Task<Fin<SpecifyOutcome>>> work,
        CancellationToken cancellationToken
    ) =>
        item switch
        {
            { IsAnswered: true } => this.ResolveThen(item.Id, work, cancellationToken),
            { IsWaiting: true } => Task.FromResult(Fin.Succ<SpecifyOutcome>(new StillWaiting())),
            _ => work(),
        };

    private async Task<Fin<SpecifyOutcome>> ResolveThen(
        WorkItemId id,
        Func<Task<Fin<SpecifyOutcome>>> work,
        CancellationToken cancellationToken
    )
    {
        Fin<Unit> resolved = await this._work.Resolve(id, cancellationToken);
        return await resolved.Then(_ => work());
    }

    private Task<Fin<SpecifyOutcome>> Specify(
        WorkItem item,
        string workingDirectory,
        CancellationToken cancellationToken
    ) =>
        this.Attempt(SpecifyAttempt.First(item, workingDirectory), cancellationToken);

    private async Task<Fin<SpecifyOutcome>> Attempt(SpecifyAttempt attempt, CancellationToken cancellationToken)
    {
        Fin<AgentResult> result = await this._agent.Run(SpecifierTask(attempt), cancellationToken);
        Fin<SpecifierAnswer> answer = result.Bind(ReadAnswer);
        return await answer.Then(read => this.ActOn(attempt, read, cancellationToken));
    }

    private Task<Fin<SpecifyOutcome>> ActOn(
        SpecifyAttempt attempt,
        SpecifierAnswer answer,
        CancellationToken cancellationToken
    ) =>
        answer switch
        {
            { Outcome: SpecifierAnswer.SpecifiedOutcome, Specification: { Length: > 0 } specification } =>
                this.WriteThenCheck(attempt, specification, cancellationToken),
            { Outcome: SpecifierAnswer.QuestionsOutcome, Questions: { Count: > 0 } questions } =>
                this.Ask(attempt.Item.Id, questions, cancellationToken),
            _ => Task.FromResult(
                Fin.Fail<SpecifyOutcome>(
                    new SpecifierAnswerUnusable($"the outcome '{answer.Outcome}' came without its content")
                )
            ),
        };

    private async Task<Fin<SpecifyOutcome>> WriteThenCheck(
        SpecifyAttempt attempt,
        string specification,
        CancellationToken cancellationToken
    )
    {
        Specified written = await WriteSpecification(attempt, specification, cancellationToken);
        Fin<Unit> checkedByGates = await this._gate.Check(attempt.WorkingDirectory, cancellationToken);
        return await checkedByGates.Match(
            Succ: _ => Task.FromResult(Fin.Succ<SpecifyOutcome>(written)),
            Fail: failure => attempt.MayRepair
                ? this.Attempt(attempt.Repairing(specification, failure.Message), cancellationToken)
                : Task.FromResult(Fin.Fail<SpecifyOutcome>(failure))
        );
    }

    private async Task<Fin<SpecifyOutcome>> Ask(
        WorkItemId id,
        IReadOnlyList<string> questions,
        CancellationToken cancellationToken
    )
    {
        Fin<Unit> asked = await this._work.Ask(id, DescribeQuestions(questions), cancellationToken);
        return asked.Map(SpecifyOutcome (_) => new AwaitingAnswers(questions));
    }
}