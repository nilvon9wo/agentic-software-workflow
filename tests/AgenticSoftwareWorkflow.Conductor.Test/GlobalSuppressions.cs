using System.Diagnostics.CodeAnalysis;

[assembly: Fody.ConfigureAwait(false)]
[assembly: SuppressMessage(
    "ConfigureAwait",
    "ConfigureAwaitEnforcer:ConfigureAwaitEnforcer",
    Justification = "ConfigureAwait.Fody applies ConfigureAwait(false) after compilation, "
        + "so analyzers that look for the call at every await cannot see it."
)]
[assembly: SuppressMessage(
    "Usage",
    "CA2007",
    Justification = "ConfigureAwait.Fody applies ConfigureAwait(false) after compilation, "
        + "so analyzers that look for the call at every await cannot see it."
)]