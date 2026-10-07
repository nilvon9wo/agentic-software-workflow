using System.Text.Json;
using AgenticSoftwareWorkflow.Conductor.Git;
using JetBrains.Annotations;
using LanguageExt;

namespace AgenticSoftwareWorkflow.Cli;

/// <summary>
/// How the conductor is set up for one repository, read from <c>aswf.json</c>
/// at the repository's root: where the work lives, who may answer for it, who
/// commits on the workers' behalf, and the command that checks the documents
/// the workers write (<c>documentGate</c>, as an argument list).
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)] // created by the JSON deserializer
internal sealed record ConductorSettings(
    string Repository,
    string BaseBranch,
    List<string> Maintainers,
    GitIdentity CommitAuthor,
    List<string>? DocumentGate
)
{
    public const string FileName = "aswf.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Fin<ConductorSettings> Load(string repositoryRoot)
    {
        string path = Path.Combine(repositoryRoot, FileName);
        Try<ConductorSettings?> read = Try.lift(
            () => JsonSerializer.Deserialize<ConductorSettings>(File.ReadAllText(path), JsonOptions)
        );
        return read
            .ToFin()
            .MapFail(error => new SettingsUnreadable(path, error.Message))
            .Bind(settings => Require(settings, path));
    }

    // A proposal must pass the project's checks, so settings that name none are
    // refused rather than run unchecked.
    private static Fin<ConductorSettings> Require(ConductorSettings? settings, string path) =>
        settings switch
        {
            null => Fin.Fail<ConductorSettings>(new SettingsUnreadable(path, "the file is the JSON literal null")),
            { DocumentGate: null or [] } => Fin.Fail<ConductorSettings>(
                new SettingsUnreadable(path, "it names no documentGate, the command that checks written documents")
            ),
            _ => Fin.Succ(settings),
        };
}