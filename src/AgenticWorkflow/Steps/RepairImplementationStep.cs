using System.Text.Json;
using AgenticWorkflow.Ai;
using AgenticWorkflow.Artifacts;
using AgenticWorkflow.Workflows;

namespace AgenticWorkflow.Steps;

public sealed class RepairImplementationStep(
    IAiProvider ai) : IWorkflowStep
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly IAiProvider _ai = ai;

    public string Name => "repair-implementation";

    public async Task<StepResult> ExecuteAsync(
        WorkflowContext context,
        CancellationToken cancellationToken)
    {
        Artifact specification =
            await context.GetAsync(
                "generated/specification.json",
                cancellationToken);

        string sourceRoot =
            Path.Combine(
                context.WorkspaceRoot,
                "generated",
                "source");

        string source =
            ReadSourceFiles(sourceRoot);

        string visibleTestsRoot =
            Path.Combine(
                context.WorkspaceRoot,
                "visible-tests");

        string visibleTests =
            ReadSourceFiles(visibleTestsRoot);

        string hiddenTestsRoot =
            Path.Combine(
                context.WorkspaceRoot,
                "hidden-tests");

        string hiddenTests =
            ReadSourceFiles(hiddenTestsRoot);

        string failureInformation =
            ReadCommandResults(context.WorkspaceRoot);

        AiRequest request = new(
            """
            You are an implementation repair agent.

            Repair ONLY the generated implementation.

            The specification is authoritative.

            The tests are evidence of required behavior.

            Do not modify the specification.

            Do not modify visible tests.

            Do not modify hidden tests.

            Do not weaken, remove, bypass, or rewrite tests to make them pass.

            Do not change the public API unless the specification explicitly
            requires that API.

            Analyze the supplied specification, implementation, tests, and
            deterministic build/test failures.

            Determine the smallest correct implementation change that makes the
            implementation conform to the specification and pass the tests.

            Return exactly one valid JSON object and nothing else:

            {
              "files": [
                {
                  "path": "...",
                  "content": "..."
                }
              ],
              "summary": "..."
            }

            The files array must contain the complete contents of every
            implementation file that should exist after the repair.

            Paths must be relative to the generated source root.

            Do not return tests.

            Do not return build artifacts.

            Do not return bin or obj files.

            Do not use absolute paths.

            Do not use '..' path segments.

            Before returning the response verify that:

            - the implementation satisfies the normative specification;
            - the implementation preserves the required public API;
            - the deterministic failure is addressed;
            - no test has been weakened or bypassed;
            - the returned JSON is valid.
            """,
            $"SPECIFICATION:\n{specification.Content}" +
            $"\n\nIMPLEMENTATION:\n{source}" +
            $"\n\nVISIBLE TESTS:\n{visibleTests}" +
            $"\n\nHIDDEN TESTS:\n{hiddenTests}" +
            $"\n\nDETERMINISTIC BUILD/TEST RESULTS:\n{failureInformation}",
            6000);

        AiResponse response =
            await this._ai.CompleteAsync(
                request,
                cancellationToken);

        RepairResult repair =
            JsonFileParser.Parse<RepairResult>(
                response.Text);

        if (repair.Files.Count == 0)
        {
            return StepResult.Failure(
                "The repair agent returned no implementation files.");
        }

        string generatedRoot =
            Path.Combine(
                context.WorkspaceRoot,
                "generated",
                "source");

        DeleteDirectoryIfExists(generatedRoot);

        _ = Directory.CreateDirectory(
            generatedRoot);

        foreach (GeneratedFile file in repair.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ValidatePath(file.Path);

            string relativePath =
                file.Path.Replace(
                    '/',
                    Path.DirectorySeparatorChar);

            string fullPath =
                Path.Combine(
                    generatedRoot,
                    relativePath);

            string? directory =
                Path.GetDirectoryName(fullPath);

            if (directory is not null)
            {
                _ = Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(
                fullPath,
                file.Content,
                cancellationToken);

            await context.SaveAsync(
                new Artifact(
                    file.Path,
                    GetMediaType(file.Path),
                    "generated/source/" + file.Path,
                    file.Content),
                cancellationToken);
        }

        await context.SaveAsync(
            new Artifact(
                "implementation-repair",
                "application/json",
                "quality/implementation-repair.json",
                JsonSerializer.Serialize(
                    repair,
                    JsonSerializerOptions)),
            cancellationToken);

        return StepResult.RetryFrom(
            $"Implementation repaired using {response.Model}: " +
            repair.Summary,
            "materialize-generated-source");
    }

    private static string ReadCommandResults(
        string workspaceRoot)
    {
        string[] resultFiles =
        [
            Path.Combine(
                workspaceRoot,
                "visible-tests.result.txt"),

            Path.Combine(
                workspaceRoot,
                "hidden-tests.result.txt"),

            Path.Combine(
                workspaceRoot,
                "build-product.result.txt")
        ];

        List<string> results = [];

        foreach (string file in resultFiles)
        {
            if (!File.Exists(file))
            {
                continue;
            }

            results.Add(
                $"{Path.GetFileName(file)}\n" +
                File.ReadAllText(file));
        }

        return results.Count == 0
            ? "<no deterministic command results found>"
            : string.Join(
                "\n\n===== COMMAND RESULT =====\n",
                results);
    }

    private static string ReadSourceFiles(
        string root)
    {
        if (!Directory.Exists(root))
        {
            return "<none>";
        }

        string[] allowedExtensions =
        [
            ".cs",
            ".csproj"
        ];

        string[] files =
            [.. Directory.EnumerateFiles(
                    root,
                    "*",
                    SearchOption.AllDirectories)
                .Where(
                    file => allowedExtensions.Contains(
                        Path.GetExtension(file),
                        StringComparer.OrdinalIgnoreCase))
                .Where(
                    file => !file.Split(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar)
                        .Any(
                            part =>
                                part.Equals(
                                    "bin",
                                    StringComparison.OrdinalIgnoreCase) ||
                                part.Equals(
                                    "obj",
                                    StringComparison.OrdinalIgnoreCase)))
                .OrderBy(
                    file => file,
                    StringComparer.OrdinalIgnoreCase)];

        return files.Length == 0
            ? "<none>"
            : string.Join(
            "\n\n===== FILE =====\n",
            files.Select(
                file =>
                    $"{Path.GetRelativePath(root, file)}\n" +
                    File.ReadAllText(file)));
    }

    private static void ValidatePath(
        string path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            Path.IsPathRooted(path))
        {
            throw new InvalidOperationException(
                $"Generated repair path '{path}' is invalid.");
        }

        string normalizedPath =
            path.Replace('\\', '/');

        if (normalizedPath
            .Split('/')
            .Any(
                static segment =>
                    segment == ".."))
        {
            throw new InvalidOperationException(
                $"Generated repair path '{path}' contains '..'.");
        }
    }

    private static string GetMediaType(
        string path)
        => Path.GetExtension(path).Equals(
            ".csproj",
            StringComparison.OrdinalIgnoreCase)
            ? "application/xml"
            : "text/x-csharp";

    private static void DeleteDirectoryIfExists(
        string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(
                path,
                true);
        }
    }

    private sealed record RepairResult(
        IReadOnlyList<GeneratedFile> Files,
        string Summary);

    private sealed record GeneratedFile(
        string Path,
        string Content);
}