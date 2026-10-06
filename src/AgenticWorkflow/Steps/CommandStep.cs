using System.Diagnostics;
using AgenticWorkflow.Workflows;

namespace AgenticWorkflow.Steps;

public sealed class CommandStep(
    string name,
    string command,
    string arguments,
    string workingDirectory) : IWorkflowStep
{
    private readonly string _command = command;
    private readonly string _arguments = arguments;
    private readonly string _workingDirectory = workingDirectory;

    public string Name { get; } = name;

    public async Task<StepResult> ExecuteAsync(
        WorkflowContext context,
        CancellationToken cancellationToken)
    {
        using Process process = new();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = this._command,
            Arguments = this._arguments,
            WorkingDirectory = this._workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _ = process.Start();

        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        string stdout = await stdoutTask;
        string stderr = await stderrTask;

        string output = $"Exit code: {process.ExitCode}\n\nSTDOUT:\n{stdout}\n\nSTDERR:\n{stderr}";
        await File.WriteAllTextAsync(
            Path.Combine(this._workingDirectory, $"{this.Name}.result.txt"),
            output,
            cancellationToken);

        return process.ExitCode == 0
            ? StepResult.Success(output)
            : StepResult.Failure(output);
    }
}
