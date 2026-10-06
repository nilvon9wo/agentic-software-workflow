using AgenticSoftwareWorkflow.Conductor.Agents;
using AgenticSoftwareWorkflow.Conductor.Claude;
using AgenticSoftwareWorkflow.Conductor.Processes;
using LanguageExt;
using NSubstitute;
using static AgenticSoftwareWorkflow.Conductor.Test.Support.FinAssertions;

namespace AgenticSoftwareWorkflow.Conductor.Test.Claude;

public sealed class ClaudeCodeAgentRunnerTest
{
    private const string Prompt = "Review the change.";
    private const string WorkingDirectory = "/repository";
    private const string SuccessEnvelope = """{ "type": "result", "is_error": false, "result": "Looks good." }""";

    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);
    private static readonly AgentRole Reviewer =
        new(CapabilityTier.Standard, [AgentTool.ReadFiles, AgentTool.SearchFiles], AgentAccess.ToolsOnly);

    private readonly IProcessCapable _processRunner = Substitute.For<IProcessCapable>();

    public ClaudeCodeAgentRunnerTest() =>
        this._processRunner
            .Run(Arg.Any<ProcessRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessOutcome(0, SuccessEnvelope, "", false));

    [Fact]
    public async Task Run_WhenGivenATask_RunsClaudeWithThePromptOnStandardInput()
    {
        // Arrange
        IAgentic runner = new ClaudeCodeAgentRunner(this._processRunner);
        AgentTask task = new(Reviewer, Prompt, WorkingDirectory, Timeout);

        // Act
        _ = await runner.Run(task, TestContext.Current.CancellationToken);

        // Assert
        ProcessRequest request = this.SentRequest();
        Assert.Equal(
            ("claude", Prompt, WorkingDirectory, Timeout),
            (request.Executable, request.StandardInput, request.WorkingDirectory, request.Timeout)
        );
    }

    [Fact]
    public async Task Run_WhenTheRoleCannotEdit_GrantsOnlyItsToolsAndAsksForNothing()
    {
        // Arrange
        IAgentic runner = new ClaudeCodeAgentRunner(this._processRunner);
        AgentTask task = new(Reviewer, Prompt, WorkingDirectory, Timeout);

        // Act
        _ = await runner.Run(task, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [
                "--print",
                "--output-format",
                "stream-json",
                "--verbose",
                "--no-session-persistence",
                "--strict-mcp-config",
                "--setting-sources",
                "project",
                "--model",
                "sonnet",
                "--tools",
                "Read,Grep,Glob",
                "--permission-mode",
                "dontAsk",
            ],
            this.SentRequest().Arguments
        );
    }

    [Fact]
    public async Task Run_WhenTheRoleCanEdit_AcceptsEdits()
    {
        // Arrange
        AgentRole implementer = new(CapabilityTier.Standard, [AgentTool.EditFiles], AgentAccess.ToolsOnly);
        IAgentic runner = new ClaudeCodeAgentRunner(this._processRunner);
        AgentTask task = new(implementer, Prompt, WorkingDirectory, Timeout);

        // Act
        _ = await runner.Run(task, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["--permission-mode", "acceptEdits"], this.SentRequest().Arguments.TakeLast(2));
    }

    [Fact]
    public async Task Run_WhenTheRoleHasNoTools_DisablesEveryTool()
    {
        // Arrange
        AgentRole summariser = new(CapabilityTier.Small, [], AgentAccess.ToolsOnly);
        IAgentic runner = new ClaudeCodeAgentRunner(this._processRunner);
        AgentTask task = new(summariser, Prompt, WorkingDirectory, Timeout);

        // Act
        _ = await runner.Run(task, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["--tools", "", "--permission-mode", "dontAsk"], this.SentRequest().Arguments.TakeLast(4));
    }

    [Fact]
    public async Task Run_WhenTheTaskHasAnOutputSchema_PassesItOn()
    {
        // Arrange
        IAgentic runner = new ClaudeCodeAgentRunner(this._processRunner);
        AgentTask task = new AgentTask(Reviewer, Prompt, WorkingDirectory, Timeout)
            .WithOutputSchema("{\"type\":\"object\"}");

        // Act
        _ = await runner.Run(task, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["--json-schema", "{\"type\":\"object\"}"], this.SentRequest().Arguments.TakeLast(2));
    }

    [Fact]
    public async Task Run_WhenTheRoleHasAccessRules_PassesThemAsSettings()
    {
        // Arrange
        AgentRole restricted = new(
            CapabilityTier.Standard,
            [AgentTool.ReadFiles],
            new AgentAccess(["hidden-tests/**"], [], [])
        );
        IAgentic runner = new ClaudeCodeAgentRunner(this._processRunner);
        AgentTask task = new(restricted, Prompt, WorkingDirectory, Timeout);

        // Act
        _ = await runner.Run(task, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ["--settings", """{"permissions":{"allow":[],"deny":["Read(./hidden-tests/**)"]}}"""],
            this.SentRequest().Arguments.TakeLast(2)
        );
    }

    [Fact]
    public async Task Run_WhenTheTaskHasInstructions_AppendsThemBeforeTheSchema()
    {
        // Arrange
        IAgentic runner = new ClaudeCodeAgentRunner(this._processRunner);
        AgentTask task = new AgentTask(Reviewer, Prompt, WorkingDirectory, Timeout)
            .WithInstructions("Review carefully.")
            .WithOutputSchema("{}");

        // Act
        _ = await runner.Run(task, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ["--append-system-prompt", "Review carefully.", "--json-schema", "{}"],
            this.SentRequest().Arguments.TakeLast(4)
        );
    }

    [Fact]
    public async Task Run_WhenGivenAnExecutable_RunsThatExecutable()
    {
        // Arrange
        IAgentic runner = new ClaudeCodeAgentRunner(this._processRunner, "/opt/claude/bin/claude");
        AgentTask task = new(Reviewer, Prompt, WorkingDirectory, Timeout);

        // Act
        _ = await runner.Run(task, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("/opt/claude/bin/claude", this.SentRequest().Executable);
    }

    [Fact]
    public async Task Run_WhenClaudeSucceeds_ReturnsItsAnswer()
    {
        // Arrange
        IAgentic runner = new ClaudeCodeAgentRunner(this._processRunner);
        AgentTask task = new(Reviewer, Prompt, WorkingDirectory, Timeout);

        // Act
        Fin<AgentResult> result = await runner.Run(task, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Looks good.", AssertSuccess(result).Text);
    }

    [Fact]
    public async Task Run_WhenUsedAsDocumented_ReturnsTheAnswerOrTheReason()
    {
        // Arrange
        IProcessCapable processRunner = this._processRunner;
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        // begin-snippet: run-an-agent
        AgentRole reviewer = new(
            CapabilityTier.Standard,
            [AgentTool.ReadFiles, AgentTool.SearchFiles],
            AgentAccess.ToolsOnly
        );
        AgentTask task = new(reviewer, "Review the change.", "/repository", TimeSpan.FromMinutes(5));
        IAgentic runner = new ClaudeCodeAgentRunner(processRunner);

        // Act
        Fin<AgentResult> result = await runner.Run(task, cancellationToken);

        // Assert
        string outcome = result.Match(
            Succ: answer => answer.Text,
            Fail: error => $"failed: {error.Message}"
        );
        // end-snippet
        Assert.Equal("Looks good.", outcome);
    }

    private ProcessRequest SentRequest() =>
        (ProcessRequest)this._processRunner.ReceivedCalls().Single().GetArguments()[0]!;
}