using System.Reflection;
using System.Runtime.CompilerServices;
using AgenticSoftwareWorkflow.Conductor.Test.Support;

namespace AgenticSoftwareWorkflow.Conductor.Test.Build;

/// <summary>
/// AC-6: ConfigureAwait.Fody has woven the built assemblies, not merely been
/// referenced - read from the compiled IL of every async state machine.
/// </summary>
public sealed class ConfigureAwaitWeavingTest
{
    private const string AwaiterMethod = "GetAwaiter";
    private const string CliAssembly = "aswf";
    private const string ConductorAssembly = "AgenticSoftwareWorkflow.Conductor";
    private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Theory]
    [InlineData(CliAssembly)]
    [InlineData(ConductorAssembly)]
    public void Await_WhenAssemblyIsBuilt_HasAwaitsToInspect(string assemblyName)
    {
        // Arrange
        Assembly assembly = Assembly.Load(assemblyName);

        // Act
        IReadOnlyList<MethodInfo> awaiting = AwaitingMoveNexts(assembly);

        // Assert
        Assert.NotEmpty(awaiting);
    }

    [Theory]
    [InlineData(CliAssembly)]
    [InlineData(ConductorAssembly)]
    public void Await_WhenAssemblyIsBuilt_IsConfiguredWithConfigureAwaitFalse(string assemblyName)
    {
        // Arrange
        Assembly assembly = Assembly.Load(assemblyName);

        // Act
        IReadOnlyList<string> unconfigured = UnconfiguredStateMachines(assembly);

        // Assert
        Assert.Empty(unconfigured);
    }

    private static IReadOnlyList<MethodInfo> AwaitingMoveNexts(Assembly assembly) =>
        [.. assembly.GetTypes()
            .Where(type => typeof(IAsyncStateMachine).IsAssignableFrom(type))
            .Select(type => type.GetMethod("MoveNext", AnyInstance)!)
            .Where(method => MethodCalls.NamesCalledBy(method).Contains(AwaiterMethod))];

    private static IReadOnlyList<string> UnconfiguredStateMachines(Assembly assembly) =>
        [.. AwaitingMoveNexts(assembly)
            .Where(method => CountOf(method, AwaiterMethod) != CountOf(method, MethodCalls.ConfigureAwaitFalse))
            .Select(method => method.DeclaringType!.FullName!)];

    private static int CountOf(MethodInfo method, string name) =>
        MethodCalls.NamesCalledBy(method).Count(called => called == name);
}