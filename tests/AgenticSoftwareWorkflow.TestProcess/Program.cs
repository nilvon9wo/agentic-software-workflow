using System.Globalization;

namespace AgenticSoftwareWorkflow.TestProcess;

/// <summary>
/// A stand-in child process. The behaviour is chosen by the first argument:
/// <c>echo</c> copies standard input to standard output; <c>fail &lt;code&gt;</c>
/// writes "failed" to standard error and exits with that code; <c>hang</c>
/// waits until it is killed.
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
            EchoCommand => await EchoAsync(),
            FailCommand => await FailAsync(arguments[1]),
            HangCommand => await HangAsync(),
            _ => await RejectAsync(command),
        };
    }

    private static async Task<int> EchoAsync()
    {
        string input = await Console.In.ReadToEndAsync();
        await Console.Out.WriteAsync(input);
        return 0;
    }

    private static async Task<int> FailAsync(string exitCode)
    {
        await Console.Error.WriteAsync("failed");
        return int.Parse(exitCode, CultureInfo.InvariantCulture);
    }

    private static async Task<int> HangAsync()
    {
        await Task.Delay(Timeout.Infinite);
        return 0;
    }

    private static async Task<int> RejectAsync(string command)
    {
        await Console.Error.WriteAsync($"unknown command '{command}'");
        return UsageExitCode;
    }
}