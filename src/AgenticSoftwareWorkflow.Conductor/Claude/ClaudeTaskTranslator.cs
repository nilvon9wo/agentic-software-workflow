using AgenticSoftwareWorkflow.Conductor.Agents;

namespace AgenticSoftwareWorkflow.Conductor.Claude;

/// <summary>
/// Translates a vendor-neutral <see cref="AgentTask"/> into the `claude -p`
/// command line that carries it out.
/// </summary>
internal static class ClaudeTaskTranslator
{
    /// <remarks>
    /// A role that may edit runs in <see cref="ClaudePermissionMode.AcceptEdits"/>;
    /// every other role in <see cref="ClaudePermissionMode.DontAsk"/>, where
    /// anything not explicitly allowed is unavailable rather than prompted for.
    /// </remarks>
    public static ClaudeInvocation ToInvocation(AgentTask task)
    {
        string model = ClaudeCapabilities.ModelFor(task.Role.Tier);
        ClaudeInvocation headless = ClaudeInvocation.Headless(model);
        ClaudeInvocation withTools = WithTools(headless, task.Role.Tools);
        ClaudePermissionMode permissionMode = task.Role.CanEdit
            ? ClaudePermissionMode.AcceptEdits
            : ClaudePermissionMode.DontAsk;
        ClaudeInvocation withPermissionMode = withTools.WithPermissionMode(permissionMode);
        ClaudeInvocation withAccess = ClaudePermissionSettings.For(task.Role.Access).Match(
            Some: withPermissionMode.WithSettings,
            None: () => withPermissionMode
        );
        ClaudeInvocation withInstructions = task.Instructions.Match(
            Some: withAccess.WithAppendedSystemPrompt,
            None: () => withAccess
        );
        return task.OutputSchema.Match(
            Some: withInstructions.WithJsonSchema,
            None: () => withInstructions
        );
    }

    private static ClaudeInvocation WithTools(ClaudeInvocation invocation, IReadOnlyList<AgentTool> tools) =>
        tools.Count == 0
            ? invocation.WithoutTools()
            : invocation.WithTools(ClaudeCapabilities.ToolNamesFor(tools));
}