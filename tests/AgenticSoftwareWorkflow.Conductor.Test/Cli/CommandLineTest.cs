using AgenticSoftwareWorkflow.Cli;
using AgenticSoftwareWorkflow.Conductor.Agents;
using AgenticSoftwareWorkflow.Conductor.Git;
using AgenticSoftwareWorkflow.Conductor.Processes;
using AgenticSoftwareWorkflow.Conductor.Work;
using AgenticSoftwareWorkflow.Conductor.Workflow;
using AgenticSoftwareWorkflow.Conductor.Workflow.Specify;
using LanguageExt;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace AgenticSoftwareWorkflow.Conductor.Test.Cli;

public sealed class CommandLineTest : IDisposable
{
    private const string Settings = """
        {
          "repository": "owner/repository",
          "baseBranch": "master",
          "maintainers": ["maintainer"],
          "commitAuthor": { "name": "repository-bot", "email": "bot@example.com" }
        }
        """;

    private const string Usage = """
        Usage: aswf specify <issue-number>   specify one issue
               aswf run [--once]             work through the ready issues, then keep watching (or stop)
        """;

    private static readonly AgentUsage NoUsage = new(0, 0, 0m, 0, []);

    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("aswf-cli-");
    private readonly StringWriter _output = new();
    private readonly IWorkSupplying _work = Substitute.For<IWorkSupplying>();
    private readonly IAgentic _agent = Substitute.For<IAgentic>();
    private readonly IProcessCapable _processes = Substitute.For<IProcessCapable>();
    private readonly List<ConductorSettings> _settingsUsed = [];

    public CommandLineTest()
    {
        _ = this._work
            .Read(Arg.Any<WorkItemId>(), Arg.Any<CancellationToken>())
            .Returns(call => Fin.Succ(new WorkItem(call.Arg<WorkItemId>(), "Title", "Body", [], [], false)));
        _ = this._work
            .Ask(Arg.Any<WorkItemId>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Fin.Succ(Unit.Default));
        _ = this._agent
            .Run(Arg.Any<AgentTask>(), Arg.Any<CancellationToken>())
            .Returns(
                Fin.Succ(
                    new AgentResult("", Prelude.Some("""{"outcome":"questions","questions":["Which?"]}"""), NoUsage, [])
                )
            );
        _ = this._processes
            .Run(Arg.Any<ProcessRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessOutcome(0, "", "", false));
    }

    [Theory]
    [InlineData]
    [InlineData("specify")]
    [InlineData("specify", "seven")]
    [InlineData("unknown", "7")]
    [InlineData("run", "--twice")]
    public async Task Run_WhenTheArgumentsAreNotUnderstood_PrintsUsage(params string[] arguments)
    {
        // Arrange
        // Nothing to arrange: the [InlineData] rows are the input; the constructor arranges the rest.

        // Act
        int exitCode = await this.Run(arguments, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            (CommandLine.UsageError, Usage + "\n"),
            (exitCode, this._output.ToString().ReplaceLineEndings("\n"))
        );
    }

    [Fact]
    public async Task Run_WhenThereAreNoSettings_FailsNamingTheSettingsFile()
    {
        // Arrange
        string path = Path.Combine(this._root.FullName, "aswf.json");

        // Act
        int exitCode = await this.Run(["specify", "7"], TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(CommandLine.Failed, exitCode);
        Assert.StartsWith(
            $"Failed (SettingsUnreadable): The conductor's settings at {path} cannot be read:",
            this._output.ToString(),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task Run_WhenTheSettingsAreJsonNull_FailsSayingSo()
    {
        // Arrange
        await this.WriteSettings("null");

        // Act
        int exitCode = await this.Run(["specify", "7"], TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(CommandLine.Failed, exitCode);
        Assert.EndsWith(
            "cannot be read: the file is the JSON literal null\n",
            this._output.ToString().ReplaceLineEndings("\n"),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task Run_WhenSpecifying_RunsTheCommandForThatIssueAndPrintsWhatHappened()
    {
        // Arrange
        await this.WriteSettings(Settings);

        // Act
        int exitCode = await this.Run(["specify", "7"], TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            (
                CommandLine.Succeeded,
                "github:owner/repository#7 is waiting on answers to 1 question(s).\n",
                "owner/repository"
            ),
            (
                exitCode,
                this._output.ToString().ReplaceLineEndings("\n"),
                Assert.Single(this._settingsUsed).Repository
            )
        );
    }

    [Fact]
    public async Task Run_WhenTheCommandFails_PrintsTheFailureAndExitsWithFailure()
    {
        // Arrange
        await this.WriteSettings(Settings);
        _ = this._processes
            .Run(Arg.Any<ProcessRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessOutcome(128, "", "fatal", false));

        // Act
        int exitCode = await this.Run(["specify", "7"], TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            (CommandLine.Failed, "Failed (CommandFailed): 'git fetch origin' exited with code 128: fatal\n"),
            (exitCode, this._output.ToString().ReplaceLineEndings("\n"))
        );
    }

    [Fact]
    public async Task Run_WhenRunningOnce_MakesOnePassAndSaysSo()
    {
        // Arrange
        await this.WriteSettings(Settings);
        _ = this._work.ListReady(Arg.Any<CancellationToken>()).Returns(Fin.Succ<IReadOnlyList<WorkItemId>>([]));

        // Act
        int exitCode = await this.Run(["run", "--once"], TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            (CommandLine.Succeeded, "[2026-10-07 12:00:00Z] 0 item(s) ready.\nThe pass is complete.\n"),
            (exitCode, this._output.ToString().ReplaceLineEndings("\n"))
        );
    }

    [Fact]
    public async Task Run_WhenRunningContinuously_KeepsGoingUntilStopped()
    {
        // Arrange
        await this.WriteSettings(Settings);
        _ = this._work.ListReady(Arg.Any<CancellationToken>()).Returns(Fin.Succ<IReadOnlyList<WorkItemId>>([]));
        using CancellationTokenSource stopped = new();
        await stopped.CancelAsync();

        // Act
        Exception? thrown = await Record.ExceptionAsync(() => this.Run(["run"], stopped.Token));

        // Assert
        _ = Assert.IsType<OperationCanceledException>(thrown, exactMatch: false);
    }

    public void Dispose()
    {
        this._output.Dispose();
        this._root.Delete(recursive: true);
    }

    private Task WriteSettings(string json) =>
        File.WriteAllTextAsync(
            Path.Combine(this._root.FullName, "aswf.json"),
            json,
            TestContext.Current.CancellationToken
        );

    private Task<int> Run(string[] arguments, CancellationToken cancellationToken) =>
        CommandLine.Run(
            arguments,
            new CommandContext(this._root.FullName, this._output, new FakeComposer(this)),
            cancellationToken
        );

    /// <summary>Builds the real commands around this test's fakes.</summary>
    private sealed class FakeComposer(CommandLineTest test) : IConductorComposing
    {
        public SpecifyCommand CreateSpecifyCommand(ConductorSettings settings, string repositoryRoot)
        {
            test._settingsUsed.Add(settings);
            GitRepository git = new(test._processes, repositoryRoot, settings.CommitAuthor);
            IChangeProposing changes = Substitute.For<IChangeProposing>();
            return new SpecifyCommand(test._work, test._agent, git, changes, settings.BaseBranch);
        }

        public RunLoop CreateRunLoop(ConductorSettings settings, CommandContext context, RunLoopOptions options) =>
            new(
                test._work,
                this.CreateSpecifyCommand(settings, context.RepositoryRoot),
                options,
                context.Output,
                new FakeTimeProvider(new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.Zero))
            );
    }
}