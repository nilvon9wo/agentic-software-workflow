using System.Collections.Frozen;
using System.Reflection;
using System.Reflection.Emit;

namespace AgenticSoftwareWorkflow.Conductor.Test.Support;

/// <summary>
/// Reads the compiled IL of a method and names what it calls, so a test can
/// see what a post-compilation weaver (ConfigureAwait.Fody) added. A call
/// preceded by <c>ldc.i4.0</c> is reported with its argument, as
/// <c>ConfigureAwait(false)</c>.
/// </summary>
internal static class MethodCalls
{
    public const string ConfigureAwaitFalse = "ConfigureAwait(false)";

    private const byte TwoByteOpCodePrefix = 0xFE;
    private const int TokenSize = 4;

    private static readonly FrozenDictionary<short, OpCode> OpCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(field => (OpCode)field.GetValue(null)!)
        .ToFrozenDictionary(code => code.Value);

    public static IReadOnlyList<string> NamesCalledBy(MethodBase method)
    {
        byte[] il = method.GetMethodBody()!.GetILAsByteArray()!;
        List<string> names = [];
        OpCode previous = OpCodes.Nop;
        int position = 0;
        while (position < il.Length)
        {
            OpCode current = ReadOpCode(il, ref position);
            if (current == OpCodes.Call || current == OpCodes.Callvirt)
            {
                names.Add(NameOfCall(method, BitConverter.ToInt32(il, position), previous));
            }

            position += OperandSize(current.OperandType, il, position);
            previous = current;
        }

        return names;
    }

    private static OpCode ReadOpCode(byte[] il, ref int position)
    {
        byte first = il[position];
        position += 1;
        if (first != TwoByteOpCodePrefix)
        {
            return OpCodesByValue[first];
        }

        byte second = il[position];
        position += 1;
        return OpCodesByValue[(short)((first << 8) | second)];
    }

    private static string NameOfCall(MethodBase caller, int token, OpCode previous)
    {
        Type[] typeArguments = caller.DeclaringType!.GetGenericArguments();
        string name = caller.Module.ResolveMethod(token, typeArguments, null)!.Name;
        bool isFalseArgument = previous == OpCodes.Ldc_I4_0;
        return name == "ConfigureAwait" && isFalseArgument ? ConfigureAwaitFalse : name;
    }

    private static int OperandSize(OperandType type, byte[] il, int position) =>
        type switch
        {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineI8 or OperandType.InlineR => 8,
            OperandType.InlineSwitch => TokenSize + (TokenSize * BitConverter.ToInt32(il, position)),
            _ => TokenSize,
        };
}