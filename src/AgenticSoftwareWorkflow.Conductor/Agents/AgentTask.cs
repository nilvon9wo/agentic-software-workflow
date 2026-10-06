using LanguageExt;

namespace AgenticSoftwareWorkflow.Conductor.Agents;

/// <summary>
/// One piece of work for one role: the prompt, where the work happens, and how
/// long it may take. Immutable: each <c>With*</c> method derives a new task.
/// </summary>
public sealed class AgentTask
{
    public AgentTask(AgentRole role, string prompt, string workingDirectory, TimeSpan timeout)
        : this(role, prompt, workingDirectory, timeout, Option<string>.None, Option<string>.None)
    {
    }

    private AgentTask(
        AgentRole role,
        string prompt,
        string workingDirectory,
        TimeSpan timeout,
        Option<string> outputSchema,
        Option<string> instructions
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        this.Role = role;
        this.Prompt = prompt;
        this.WorkingDirectory = workingDirectory;
        this.Timeout = timeout;
        this.OutputSchema = outputSchema;
        this.Instructions = instructions;
    }

    public AgentRole Role { get; }

    public string Prompt { get; }

    public string WorkingDirectory { get; }

    public TimeSpan Timeout { get; }

    /// <summary>
    /// A JSON Schema the answer must satisfy, when the conductor needs a contract
    /// it can deserialize rather than prose.
    /// </summary>
    public Option<string> OutputSchema { get; }

    /// <summary>
    /// The role's standing procedure, applied on every run. Unlike a skill, which
    /// the model loads only when it judges it relevant, instructions cannot be
    /// skipped — right for a worker whose whole job is that procedure.
    /// </summary>
    public Option<string> Instructions { get; }

    public AgentTask WithOutputSchema(string jsonSchema)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jsonSchema);
        return new AgentTask(
            this.Role,
            this.Prompt,
            this.WorkingDirectory,
            this.Timeout,
            jsonSchema,
            this.Instructions
        );
    }

    public AgentTask WithInstructions(string instructions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instructions);
        return new AgentTask(
            this.Role,
            this.Prompt,
            this.WorkingDirectory,
            this.Timeout,
            this.OutputSchema,
            instructions
        );
    }
}