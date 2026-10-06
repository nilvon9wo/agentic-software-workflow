using System.Text.Json;
using AgenticSoftwareWorkflow.Conductor.Git;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;

namespace AgenticSoftwareWorkflow.Cli;

/// <summary>
/// How the conductor is set up for one repository, read from <c>aswf.json</c>
/// at the repository's root: where the work lives, who may answer for it, and
/// who commits on the workers' behalf.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)] // created by the JSON deserializer
internal sealed record ConductorSettings(
    string Repository,
    string BaseBranch,
    List<string> Maintainers,
    GitIdentity CommitAuthor
)
{
    public const string FileName = "aswf.json";
    public const int UnreadableCode = 5001;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Fin<ConductorSettings> Load(string repositoryRoot)
    {
        string path = Path.Combine(repositoryRoot, FileName);
        Try<ConductorSettings?> read = Try.lift(
            () => JsonSerializer.Deserialize<ConductorSettings>(File.ReadAllText(path), JsonOptions)
        );
        return read.ToFin().MapFail(error => Unreadable(path, error.Message)).Bind(settings => Require(settings, path));
    }

    private static Fin<ConductorSettings> Require(ConductorSettings? settings, string path) =>
        settings is null
            ? Fin.Fail<ConductorSettings>(Unreadable(path, "the file is the JSON literal null"))
            : Fin.Succ(settings);

    private static Error Unreadable(string path, string reason) =>
        Error.New(UnreadableCode, $"The conductor's settings at {path} cannot be read: {reason}");
}