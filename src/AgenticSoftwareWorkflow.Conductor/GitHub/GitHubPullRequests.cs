using AgenticSoftwareWorkflow.Conductor.Functional;
using AgenticSoftwareWorkflow.Conductor.Processes;
using AgenticSoftwareWorkflow.Conductor.Work;
using LanguageExt;

namespace AgenticSoftwareWorkflow.Conductor.GitHub;

/// <summary>
/// Proposes changes as GitHub pull requests, with auto-merge requested: each
/// merges as soon as the repository's rules are met — immediately once the
/// gates pass for ordinary changes, and only after a maintainer's approval
/// for changes the rules reserve to them, such as specifications.
/// </summary>
public sealed class GitHubPullRequests(IProcessCapable processes, GitHubOptions options, string baseBranch)
    : IChangeProposing
{
    private const string ReadFromStandardInput = "-";

    private readonly CommandLineTool _gh = new(processes, options.Executable, options.WorkingDirectory);
    private readonly GitHubOptions _options = options;
    private readonly string _baseBranch = baseBranch;

    public async Task<Fin<string>> Propose(ChangeProposal proposal, CancellationToken cancellationToken)
    {
        Fin<string> created = await this._gh.Run(
            [
                "pr",
                "create",
                "--repo",
                this._options.Repository,
                "--base",
                this._baseBranch,
                "--head",
                proposal.Branch,
                "--title",
                proposal.Title,
                "--body-file",
                ReadFromStandardInput,
            ],
            proposal.Description,
            cancellationToken
        );
        Fin<string> address = created.Map(output => output.Trim());
        Fin<string> merging = await address.Then(
            url => this._gh.Run(["pr", "merge", url, "--auto", "--merge"], cancellationToken)
        );
        return merging.Bind(_ => address);
    }
}