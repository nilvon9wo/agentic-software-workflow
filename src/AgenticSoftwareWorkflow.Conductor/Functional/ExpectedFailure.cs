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
/// A failure exposes only its message until a handler needs more: a property
/// is added when some caller's behaviour depends on it, and tested through
/// that behaviour.
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