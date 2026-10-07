using System.Text; // expect: inspect:RedundantUsingDirective

namespace StyleCanary;

public sealed class Violations
{
    private int count; // expect: build:IDE1006
    private readonly int _unset = 0; // expect: format:CA1805

    public int Next(int amount)
    {
        var doubled = amount * 2; // expect: build:IDE0008
        return Bump(doubled); // expect: build:IDE0009
    }

    public int Total(int first, int second, int third, int fourth, int fifth, int sixth) => this.count + first + second; // expect: layout:line-length

    private int Bump(int amount)
    {
        return this.count + amount; // expect: build:IDE0022
    }

    public int Sum(int first,
        int second) => first + second; // expect: layout:wrap-rpar

    public int Spaced() => this.count  +  1; // expect: format:WHITESPACE

    public int Guarded()
    {
        try
        {
            return this.count;
        }
        catch (Exception) // expect: build:CA1031
        {
            return 0;
        }
    }

    public void Forget()
    {
        System.Threading.Tasks.Task.Delay(1); // expect: build:VSTHRD110
    }

    public int Accumulate(IEnumerable<int> values)
    {
        int total = this.count;
        foreach (int value in values.ToList()) // expect: build:HLQ012
        {
            total += value;
        }

        return total;
    }
}
