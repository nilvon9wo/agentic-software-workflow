using AgenticSoftwareWorkflow.Cli;
using AgenticSoftwareWorkflow.Conductor.Agents;
using AgenticSoftwareWorkflow.Conductor.Git;
using AgenticSoftwareWorkflow.Conductor.Processes;
using AgenticSoftwareWorkflow.Conductor.Work;
using AgenticSoftwareWorkflow.Conductor.Workflow.Specify;
using LanguageExt;
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
    public async Task Run_WhenTheArgumentsAreNotUnderstood_PrintsUsage(params string[] arguments)
    {
        // Arrange
        // Nothing to arrange: the [InlineData] rows are the input; the constructor arranges the rest.

        // Act
        int exitCode = await this.Run(arguments);

        // Assert
        Assert.Equal(
            (CommandLine.UsageError, "Usage: aswf specify <issue-number>\n"),
            (exitCode, this._output.ToString().ReplaceLineEndings("\n"))
        );
    }

    [Fact]
    public async Task Run_WhenThereAreNoSettings_FailsNamingTheSettingsFile()
    {
        // Arrange
        string path = Path.Combine(this._root.FullName, "aswf.json");

        // Act
        int exitCode = await this.Run(["specify", "7"]);

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
        int exitCode = await this.Run(["specify", "7"]);

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
        int exitCode = await this.Run(["specify", "7"]);

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
        int exitCode = await this.Run(["specify", "7"]);

        // Assert
        Assert.Equal(
            (CommandLine.Failed, "Failed (CommandFailed): 'git fetch origin' exited with code 128: fatal\n"),
            (exitCode, this._output.ToString().ReplaceLineEndings("\n"))
        );
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

    private Task<int> Run(string[] arguments) =>
        CommandLine.Run(
            arguments,
            this._root.FullName,
            this._output,
            this.CreateSpecifyCommand,
            TestContext.Current.CancellationToken
        );

    private SpecifyCommand CreateSpecifyCommand(ConductorSettings settings, string repositoryRoot)
    {
        this._settingsUsed.Add(settings);
        GitRepository git = new(this._processes, repositoryRoot, settings.CommitAuthor);
        IChangeProposing changes = Substitute.For<IChangeProposing>();
        return new SpecifyCommand(this._work, this._agent, git, changes, settings.BaseBranch);
    }
}