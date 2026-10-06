using LanguageExt;
using LanguageExt.Common;

namespace AgenticSoftwareWorkflow.Conductor.Functional;

/// <summary>
/// The base of every failure this project expects and handles. Each kind of
/// failure is its own type, carrying its own data, so callers tell failures
/// apart by type and respond polymorphically — never by switching on a numeric
/// code, which would be branching logic in disguise.
/// </summary>
/// <remarks>
/// Each failure's data is public: it is the contract with whatever handles
/// the failure (a repair loop deciding to retry, say), hence <c>[PublicAPI]</c>.
/// </remarks>
public abstract record ExpectedFailure : Expected
{
    /// <summary>LanguageExt's numeric code, deliberately unused: the type is the identity.</summary>
    private const int NotCoded = 0;

    protected ExpectedFailure(string message)
        : base(message, NotCoded, Option<Error>.None)
    {
    }
}