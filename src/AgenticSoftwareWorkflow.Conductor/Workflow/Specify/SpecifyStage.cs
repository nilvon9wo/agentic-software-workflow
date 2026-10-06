using System.Text;
using System.Text.Json;
using AgenticSoftwareWorkflow.Conductor.Agents;
using AgenticSoftwareWorkflow.Conductor.Functional;
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
/// and the role keeps the least authority its job needs.
/// </remarks>
public sealed class SpecifyStage(IWorkSupplying work, IAgentic agent)
{
    // GitHub content is the same whichever OS the conductor runs on.
    private const char LineBreak = '\n';

    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(15);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IWorkSupplying _work = work;
    private readonly IAgentic _agent = agent;

    public async Task<Fin<SpecifyOutcome>> Run(
        WorkItemId id,
        string workingDirectory,
        CancellationToken cancellationToken
    )
    {
        Fin<WorkItem> item = await this._work.Read(id, cancellationToken);
        return await item.Then(read => this.Begin(read, workingDirectory, cancellationToken));
    }

    private static AgentTask SpecifierTask(WorkItem item, string workingDirectory) =>
        new AgentTask(WorkflowRoles.Specifier, WorkItemBrief.Describe(item), workingDirectory, Timeout)
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

    private static async Task<Fin<SpecifyOutcome>> WriteSpecification(
        WorkItem item,
        string specification,
        string workingDirectory,
        CancellationToken cancellationToken
    )
    {
        string fileName = SpecificationFileName.For(item);
        string relativePath = $"{WorkspaceLayout.SpecificationDirectory}/{fileName}";
        string fullPath = Path.Combine(workingDirectory, WorkspaceLayout.SpecificationDirectory, fileName);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(fullPath, specification, cancellationToken);
        return Fin.Succ<SpecifyOutcome>(new Specified(relativePath));
    }

    // An answered item stops waiting before the specifier reads the answers;
    // an unanswered one is left alone, so no agent run is spent re-asking.
    private Task<Fin<SpecifyOutcome>> Begin(
        WorkItem item,
        string workingDirectory,
        CancellationToken cancellationToken
    ) =>
        item switch
        {
            { IsAnswered: true } => this.ResolveThenSpecify(item, workingDirectory, cancellationToken),
            { IsWaiting: true } => Task.FromResult(Fin.Succ<SpecifyOutcome>(new StillWaiting())),
            _ => this.Specify(item, workingDirectory, cancellationToken),
        };

    private async Task<Fin<SpecifyOutcome>> ResolveThenSpecify(
        WorkItem item,
        string workingDirectory,
        CancellationToken cancellationToken
    )
    {
        Fin<Unit> resolved = await this._work.Resolve(item.Id, cancellationToken);
        return await resolved.Then(_ => this.Specify(item, workingDirectory, cancellationToken));
    }

    private async Task<Fin<SpecifyOutcome>> Specify(
        WorkItem item,
        string workingDirectory,
        CancellationToken cancellationToken
    )
    {
        Fin<AgentResult> result = await this._agent.Run(SpecifierTask(item, workingDirectory), cancellationToken);
        Fin<SpecifierAnswer> answer = result.Bind(ReadAnswer);
        return await answer.Then(read => this.ActOn(item, read, workingDirectory, cancellationToken));
    }

    private Task<Fin<SpecifyOutcome>> ActOn(
        WorkItem item,
        SpecifierAnswer answer,
        string workingDirectory,
        CancellationToken cancellationToken
    ) =>
        answer switch
        {
            { Outcome: SpecifierAnswer.SpecifiedOutcome, Specification: { Length: > 0 } specification } =>
                WriteSpecification(item, specification, workingDirectory, cancellationToken),
            { Outcome: SpecifierAnswer.QuestionsOutcome, Questions: { Count: > 0 } questions } =>
                this.Ask(item.Id, questions, cancellationToken),
            _ => Task.FromResult(
                Fin.Fail<SpecifyOutcome>(
                    new SpecifierAnswerUnusable($"the outcome '{answer.Outcome}' came without its content")
                )
            ),
        };

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