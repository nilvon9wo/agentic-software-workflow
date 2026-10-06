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
    private const string StreamJsonOutputFormat = "stream-json";
    private const string VerboseFlag = "--verbose";
    private const string NoSessionPersistenceFlag = "--no-session-persistence";
    private const string StrictMcpConfigFlag = "--strict-mcp-config";
    private const string SettingSourcesFlag = "--setting-sources";
    private const string ProjectSettingsOnly = "project";
    private const string ModelFlag = "--model";
    private const string ToolsFlag = "--tools";
    private const string NoTools = "";
    private const string ToolSeparator = ",";
    private const string PermissionModeFlag = "--permission-mode";
    private const string SettingsFlag = "--settings";
    private const string JsonSchemaFlag = "--json-schema";
    private const string AppendSystemPromptFlag = "--append-system-prompt";

    private ClaudeInvocation(IReadOnlyList<string> arguments) => this.Arguments = arguments;

    /// <summary>The arguments to pass to the <c>claude</c> executable, in order.</summary>
    public IReadOnlyList<string> Arguments { get; }

    /// <summary>
    /// A non-interactive, non-persisted, isolated run that reports as a stream of
    /// JSON messages ending in its result, so the conductor parses a contract
    /// rather than scraping prose. The stream (which needs <c>--verbose</c>) is
    /// what carries rate-limit reports, so a usage limit is read, not guessed.
    /// </summary>
    /// <remarks>
    /// Isolated means the run sees only what its role grants: no MCP servers
    /// beyond those passed explicitly (otherwise it inherits every connector the
    /// user has configured), and no user or local settings files (otherwise a
    /// broad "allow" rule in the user's own settings would widen every role).
    /// </remarks>
    public static ClaudeInvocation Headless(string model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        return new ClaudeInvocation(
            [
                PrintFlag,
                OutputFormatFlag,
                StreamJsonOutputFormat,
                VerboseFlag,
                NoSessionPersistenceFlag,
                StrictMcpConfigFlag,
                SettingSourcesFlag,
                ProjectSettingsOnly,
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
    /// Layers a role's settings (permission allow/deny rules, hooks) on top of
    /// the project's — either a settings file's path or the settings JSON itself.
    /// This is where separation of authority is enforced.
    /// </summary>
    public ClaudeInvocation WithSettings(string settingsFileOrJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsFileOrJson);
        return this.Append(SettingsFlag, settingsFileOrJson);
    }

    /// <summary>Constrains the final answer to a JSON Schema the conductor can deserialize.</summary>
    /// <summary>
    /// Adds a role's standing instructions to Claude Code's own system prompt,
    /// so they apply on every turn rather than only when the model chooses.
    /// </summary>
    public ClaudeInvocation WithAppendedSystemPrompt(string instructions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instructions);
        return this.Append(AppendSystemPromptFlag, instructions);
    }

    public ClaudeInvocation WithJsonSchema(string jsonSchema)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jsonSchema);
        return this.Append(JsonSchemaFlag, jsonSchema);
    }

    private ClaudeInvocation Append(string flag, string value) => new([.. this.Arguments, flag, value]);
}