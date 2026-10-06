using LanguageExt;
using LanguageExt.Common;
using Xunit.Sdk;

namespace AgenticSoftwareWorkflow.Conductor.Test.Support;

/// <summary>
/// Unwraps a <see cref="Fin{A}"/> in a test, failing with a message that shows
/// what came back instead — so a wrong outcome is diagnosed from the test
/// report, not by re-running under a debugger.
/// </summary>
internal static class FinAssertions
{
    public static T AssertSuccess<T>(Fin<T> fin) =>
        fin.Match(
            Succ: value => value,
            Fail: error => throw new XunitException($"Expected success, but failed with: {error}")
        );

    public static Error AssertFailure<T>(Fin<T> fin) =>
        fin.Match(
            Succ: value => throw new XunitException($"Expected failure, but succeeded with: {value}"),
            Fail: error => error
        );
}