namespace AgenticSoftwareWorkflow.Conductor.Claude;

/// <summary>
/// The command line for one headless <c>claude -p</c> run by one workflow role.
/// Immutable: every <c>With*</c> returns a new invocation, so a shared base
/// (model, output format) can be specialised per role without leaking options
/// from one role into another.
/// </summary>
public sealed class ClaudeInvocation
{
    private const string PrintFlag = "--print";
    private const string OutputFormatFlag = "--output-format";
    private const string JsonOutputFormat = "json";
    private const string NoSessionPersistenceFlag = "--no-session-persistence";
    private const string ModelFlag = "--model";
    private const string ToolsFlag = "--tools";
    private const string NoTools = "";
    private const string ToolSeparator = ",";
    private const string PermissionModeFlag = "--permission-mode";
    private const string SettingsFlag = "--settings";
    private const string JsonSchemaFlag = "--json-schema";

    private ClaudeInvocation(IReadOnlyList<string> arguments) => this.Arguments = arguments;

    /// <summary>The arguments to pass to the <c>claude</c> executable, in order.</summary>
    public IReadOnlyList<string> Arguments { get; }

    /// <summary>
    /// A non-interactive, non-persisted run whose result is a single JSON
    /// document, so the conductor parses a contract rather than scraping prose.
    /// </summary>
    public static ClaudeInvocation Headless(string model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        return new ClaudeInvocation(
            [
                PrintFlag,
                OutputFormatFlag,
                JsonOutputFormat,
                NoSessionPersistenceFlag,
                ModelFlag,
                model,
            ]
        );
    }

    /// <summary>
    /// Restricts the run to the named built-in tools. A role is granted what its
    /// job needs and nothing else: a reviewer gets read tools, never edit tools.
    /// </summary>
    public ClaudeInvocation WithTools(IReadOnlyList<string> toolNames)
    {
        ArgumentOutOfRangeException.ThrowIfZero(toolNames.Count, nameof(toolNames));
        string joinedToolNames = string.Join(ToolSeparator, toolNames);
        return this.Append(ToolsFlag, joinedToolNames);
    }

    /// <summary>A pure-reasoning run: the model can only answer, never act.</summary>
    public ClaudeInvocation WithoutTools() => this.Append(ToolsFlag, NoTools);

    public ClaudeInvocation WithPermissionMode(ClaudePermissionMode permissionMode)
    {
        string permissionModeArgument = ClaudePermissionModes.ToArgument(permissionMode);
        return this.Append(PermissionModeFlag, permissionModeArgument);
    }

    /// <summary>
    /// Layers a role's settings file (permission allow/deny rules, hooks) on top
    /// of the project's. This is where separation of authority is enforced.
    /// </summary>
    public ClaudeInvocation WithSettingsFile(string settingsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);
        return this.Append(SettingsFlag, settingsPath);
    }

    /// <summary>Constrains the final answer to a JSON Schema the conductor can deserialize.</summary>
    public ClaudeInvocation WithJsonSchema(string jsonSchema)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jsonSchema);
        return this.Append(JsonSchemaFlag, jsonSchema);
    }

    private ClaudeInvocation Append(string flag, string value) => new([.. this.Arguments, flag, value]);
}