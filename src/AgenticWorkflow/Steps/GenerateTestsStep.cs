using System.Text.Json;
using AgenticWorkflow.Ai;
using AgenticWorkflow.Artifacts;
using AgenticWorkflow.Testing;
using AgenticWorkflow.Workflows;

namespace AgenticWorkflow.Steps;

public sealed class GenerateTestsStep(
    IAiProvider ai,
    TestGenerationOptions options) : IWorkflowStep
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IAiProvider _ai = ai;
    private readonly TestGenerationOptions _options = options;

    public string Name => "generate-tests";

    public async Task<StepResult> ExecuteAsync(
        WorkflowContext context,
        CancellationToken cancellationToken)
    {
        Artifact specification = await context.GetAsync(
            "generated/specification.json",
            cancellationToken);

        string systemPrompt =
            """
    You are a test architect.

    Based only on the supplied specification, design a small but
    meaningful visible and hidden automated test suite.

    The visible tests are shown to the implementation AI.
    The hidden tests are not.

    The tests must verify the normative behavior of the specification,
    not merely its examples.

    Before generating each test:

    1. Identify the relevant requirement or behavioral rule.
    2. Examine the concrete test input.
    3. Independently derive the expected result from the normative
       rules in the specification.
    4. Verify that the expected result follows from those rules.
    5. Only then encode the test.

    Do not mechanically copy an expected value from a specification
    example without independently checking it.

    If a concrete example contradicts a normative rule, prefer the
    normative rule. Do not silently encode the contradiction.

    Do not test behavior that is outside the specification merely
    because it might be interesting.

    The implementation AI must be able to understand the visible tests
    without access to the hidden tests.

    Your response MUST be one valid JSON object and nothing else.

    The JSON object MUST have exactly this structure:

    {
      "visibleFiles": [
        {
          "path": "...",
          "content": "..."
        }
      ],
      "hiddenFiles": [
        {
          "path": "...",
          "content": "..."
        }
      ]
    }

    Each file object must contain exactly "path" and "content".

    PATH RULES:

    - Paths are relative to their respective package root.
    - Do not include the package directory in a path.
    - Do not use absolute paths.
    - Do not use '..' path segments.
    - Use '/' for directory separators.

    Each package must contain exactly one project file and at least
    one C# source file.

    Use the public API exactly as specified. Do not invent, simplify,
    rename, or reinterpret the API.

    API NAME RESOLUTION:

    - Treat namespaces and types as distinct symbols.
    - Never treat a namespace name as though it were a type name.
    - Fully qualify production API references whenever necessary to avoid
      namespace/type ambiguity.
    - If the production API contains a type whose name is the same as one of
      its containing namespace components, use the fully qualified type name
      when calling it.
    - Verify every production API reference against the namespace, type,
      member, parameter, and return-type information in the specification.

    NAMESPACE/TYPE COLLISION RULES:

    - Test namespaces must not have the same name as production types.
    - Test types must not have the same name as production namespaces.
    - Do not introduce namespace/type-name collisions merely to make generated
      code shorter or more convenient.
    - Avoid ambiguous namespace/type references throughout the generated tests.

    The generated tests must compile against the exact production API described
    by the specification.

    Before returning the response verify:

    - every test follows the normative specification;
    - every expected value was independently derived;
    - no tests contradict one another;
    - the C# is syntactically plausible;
    - the public API is referenced exactly as specified;
    - no namespace/type collision exists;
    - all paths are valid;
    - each package contains exactly one project;
    - each package contains at least one C# source file;
    - the complete response is valid JSON.
    """;

        systemPrompt +=
            $"""

    The visible test project must be named:
    {this._options.Visible.ProjectFileName}

    The hidden test project must be named:
    {this._options.Hidden.ProjectFileName}

    The visible test project must reference:
    {this._options.Visible.ProductProjectReference}

    The hidden test project must reference:
    {this._options.Hidden.ProductProjectReference}
    """;

        AiRequest request = new(
            systemPrompt,
            $"SPECIFICATION:\n{specification.Content}",
            6000);

        AiResponse response = await this._ai.CompleteAsync(
            request,
            cancellationToken);

        TestPackage package =
            JsonFileParser.Parse<TestPackage>(response.Text);

        ValidationResult validation = this.ValidatePackage(package);

        if (!validation.Succeeded)
        {
            return StepResult.Failure(validation.Message);
        }

        string visibleRoot = Path.Combine(
            context.WorkspaceRoot,
            this._options.Visible.ArtifactPrefix);

        string hiddenRoot = Path.Combine(
            context.WorkspaceRoot,
            this._options.Hidden.ArtifactPrefix);

        DeleteDirectoryIfExists(visibleRoot);
        DeleteDirectoryIfExists(hiddenRoot);

        _ = Directory.CreateDirectory(visibleRoot);
        _ = Directory.CreateDirectory(hiddenRoot);

        await SaveFilesAsync(
            context,
            package.VisibleFiles,
            this._options.Visible.ArtifactPrefix,
            cancellationToken);

        await SaveFilesAsync(
            context,
            package.HiddenFiles,
            this._options.Hidden.ArtifactPrefix,
            cancellationToken);

        return StepResult.Success(
            $"Generated {package.VisibleFiles.Count} visible and " +
            $"{package.HiddenFiles.Count} hidden files using {response.Model}.");
    }

    private ValidationResult ValidatePackage(TestPackage package)
    {
        ValidationResult visibleValidation =
            ValidatePackageFiles(
                package.VisibleFiles,
                this._options.Visible,
                "visible");

        return !visibleValidation.Succeeded
            ? visibleValidation
            : ValidatePackageFiles(
            package.HiddenFiles,
            this._options.Hidden,
            "hidden");
    }

    private static ValidationResult ValidatePackageFiles(
        IReadOnlyList<GeneratedFile> files,
        TestPackageOptions options,
        string packageName)
    {
        if (files.Count == 0)
        {
            return ValidationResult.Failure(
                $"The {packageName} test package contains no files.");
        }

        List<GeneratedFile> projectFiles = [.. files
            .Where(
                static file =>
                    Path.GetExtension(file.Path).Equals(
                        ".csproj",
                        StringComparison.OrdinalIgnoreCase))];

        if (projectFiles.Count != 1)
        {
            return ValidationResult.Failure(
                $"The {packageName} test package must contain exactly " +
                $"one .csproj file, but contains {projectFiles.Count}.");
        }

        List<GeneratedFile> sourceFiles = [.. files
            .Where(
                static file =>
                    Path.GetExtension(file.Path).Equals(
                        ".cs",
                        StringComparison.OrdinalIgnoreCase))];

        if (sourceFiles.Count == 0)
        {
            return ValidationResult.Failure(
                $"The {packageName} test package must contain at least " +
                "one .cs source file.");
        }

        foreach (GeneratedFile file in files)
        {
            ValidationResult pathValidation =
                ValidatePath(file.Path, packageName);

            if (!pathValidation.Succeeded)
            {
                return pathValidation;
            }

            if (string.IsNullOrWhiteSpace(file.Content))
            {
                return ValidationResult.Failure(
                    $"The {packageName} test file '{file.Path}' is empty.");
            }
        }

        GeneratedFile projectFile = projectFiles[0];

        return !projectFile.Path.Equals(
                options.ProjectFileName,
                StringComparison.OrdinalIgnoreCase)
            ? ValidationResult.Failure(
                $"The {packageName} test project must use " +
                $"'{options.ProjectFileName}'.")
            : !projectFile.Content.Contains(
                options.ProductProjectReference,
                StringComparison.Ordinal)
            ? ValidationResult.Failure(
                $"The {packageName} test project must reference " +
                $"'{options.ProductProjectReference}'.")
            : ValidationResult.Success();
    }

    private static ValidationResult ValidatePath(
        string path,
        string packageName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return ValidationResult.Failure(
                $"The {packageName} test package contains an empty path.");
        }

        if (Path.IsPathRooted(path))
        {
            return ValidationResult.Failure(
                $"The {packageName} test path '{path}' is absolute.");
        }

        string normalizedPath = path.Replace('\\', '/');

        string[] segments = normalizedPath.Split('/');

        return segments.Any(
                static segment =>
                    segment.Equals("..", StringComparison.Ordinal))
            ? ValidationResult.Failure(
                $"The {packageName} test path '{path}' contains a '..' segment.")
            : ValidationResult.Success();
    }

    private static async Task SaveFilesAsync(
        WorkflowContext context,
        IReadOnlyList<GeneratedFile> files,
        string prefix,
        CancellationToken cancellationToken)
    {
        foreach (GeneratedFile file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await context.SaveAsync(
                new Artifact(
                    file.Path,
                    GetMediaType(file.Path),
                    prefix + "/" + file.Path,
                    file.Content),
                cancellationToken);
        }
    }

    private static string GetMediaType(string path)
        => Path.GetExtension(path).Equals(
            ".csproj",
            StringComparison.OrdinalIgnoreCase)
            ? "application/xml"
            : "text/x-csharp";

    private static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }
    }

    private sealed record TestPackage(
        IReadOnlyList<GeneratedFile> VisibleFiles,
        IReadOnlyList<GeneratedFile> HiddenFiles);

    private sealed record GeneratedFile(
        string Path,
        string Content);

    private sealed record ValidationResult(
        bool Succeeded,
        string Message)
    {
        public static ValidationResult Success()
            => new(true, string.Empty);

        public static ValidationResult Failure(string message)
            => new(false, message);
    }
}