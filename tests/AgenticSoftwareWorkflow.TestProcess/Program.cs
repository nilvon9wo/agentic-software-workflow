using System.Globalization;

namespace AgenticSoftwareWorkflow.TestProcess;

/// <summary>
/// A stand-in child process. The behaviour is chosen by the first argument:
/// <c>echo</c> copies standard input to standard output; <c>fail &lt;code&gt;</c>
/// writes "failed" to standard error and exits with that code; <c>hang</c>
/// waits until it is killed, first writing its process id to the file named
/// by a second argument, if there is one, so a test can check it was stopped.
/// </summary>
internal static class Program
{
    private const string EchoCommand = "echo";
    private const string FailCommand = "fail";
    private const string HangCommand = "hang";
    private const int UsageExitCode = 64;

    public static async Task<int> Main(string[] arguments)
    {
        string command = arguments.Length > 0 ? arguments[0] : string.Empty;
        return command switch
        {
            EchoCommand => await Echo(),
            FailCommand => await Fail(arguments[1]),
            HangCommand => await Hang(arguments.Skip(1)),
            _ => await Reject(command),
        };
    }

    private static async Task<int> Echo()
    {
        string input = await Console.In.ReadToEndAsync();
        await Console.Out.WriteAsync(input);
        return 0;
    }

    private static async Task<int> Fail(string exitCode)
    {
        await Console.Error.WriteAsync("failed");
        return int.Parse(exitCode, CultureInfo.InvariantCulture);
    }

    private static async Task<int> Hang(IEnumerable<string> processIdFile)
    {
        foreach (string path in processIdFile)
        {
            await File.WriteAllTextAsync(path, $"{Environment.ProcessId}");
        }

        await Task.Delay(Timeout.Infinite);
        return 0;
    }

    private static async Task<int> Reject(string command)
    {
        await Console.Error.WriteAsync($"unknown command '{command}'");
        return UsageExitCode;
    }
}