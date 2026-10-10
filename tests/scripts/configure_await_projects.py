"""Throwaway projects for testing the configure-await gate."""

from pathlib import Path

PACKAGE_REFERENCE = (
    '    <PackageReference Include="ConfigureAwait.Fody"'
    ' PrivateAssets="all" />\n'
)
UNPRIVATE_PACKAGE_REFERENCE = (
    '    <PackageReference Include="ConfigureAwait.Fody" />\n'
)
ATTRIBUTE = "[assembly: Fody.ConfigureAwait(false)]\n"
USING = "using System.Diagnostics.CodeAnalysis;\n\n"
REAL_JUSTIFICATION = (
    "ConfigureAwait.Fody applies ConfigureAwait(false) after compilation,"
    " so analyzers that look for the call at every await cannot see it."
)
UNRELATED_JUSTIFICATION = "This is suppressed because it is noisy."
PENDING_JUSTIFICATION = "<Pending>"
EMPTY_JUSTIFICATION = ""
NOTHING = ""


def project_text(package_reference: str) -> str:
    """A project file holding `package_reference` (or none)."""
    return (
        '<Project Sdk="Microsoft.NET.Sdk">\n'
        "  <ItemGroup>\n"
        f"{package_reference}"
        "  </ItemGroup>\n"
        "</Project>\n"
    )


def enforcer_suppression(
    justification: str,
    category: str = "ConfigureAwait",
    check_id: str = "ConfigureAwaitEnforcer:ConfigureAwaitEnforcer",
) -> str:
    """The ConfigureAwaitEnforcer suppression, justified as given."""
    return (
        "[assembly: SuppressMessage(\n"
        f'    "{category}",\n'
        f'    "{check_id}",\n'
        f'    Justification = "{justification}"\n'
        ")]\n"
    )


def usage_suppression(
    justification: str,
    category: str = "Usage",
) -> str:
    """The CA2007 suppression, justified as given."""
    return (
        "[assembly: SuppressMessage(\n"
        f'    "{category}",\n'
        '    "CA2007",\n'
        f'    Justification = "{justification}"\n'
        ")]\n"
    )


REAL_ENFORCER = enforcer_suppression(REAL_JUSTIFICATION)
REAL_USAGE = usage_suppression(REAL_JUSTIFICATION)


def suppressions_text(
    attribute: str = ATTRIBUTE,
    enforcer: str = REAL_ENFORCER,
    usage: str = REAL_USAGE,
) -> str:
    """A GlobalSuppressions.cs; complete unless a part is replaced."""
    return f"{USING}{attribute}{enforcer}{usage}"


def write_project(
    root: Path,
    directory: str,
    package_reference: str = PACKAGE_REFERENCE,
    suppressions: str | None = None,
) -> None:
    """A project under `root`; complete unless told otherwise.

    `suppressions` of None writes the complete GlobalSuppressions.cs. A
    project with no such file is made with `write_project_without_file`.
    """
    project_directory = root / directory
    project_directory.mkdir(parents=True, exist_ok=True)
    project_file = project_directory / f"{project_directory.name}.csproj"
    project_file.write_text(project_text(package_reference), encoding="utf-8")
    suppressions_file = project_directory / "GlobalSuppressions.cs"
    if suppressions is None:
        suppressions_file.write_text(suppressions_text(), encoding="utf-8")
    else:
        suppressions_file.write_text(suppressions, encoding="utf-8")


def write_project_without_file(root: Path, directory: str) -> None:
    """A project that has no GlobalSuppressions.cs at all."""
    write_project(root, directory)
    suppressions_file = root / directory / "GlobalSuppressions.cs"
    suppressions_file.unlink()


def package_version(version: str, name: str = "ConfigureAwait.Fody") -> str:
    """One `PackageVersion` entry for `name`."""
    return f'    <PackageVersion Include="{name}" Version="{version}" />\n'


def packages_text(entries: str) -> str:
    """A Directory.Packages.props holding `entries`."""
    return (
        "<Project>\n"
        "  <ItemGroup>\n"
        f"{entries}"
        "  </ItemGroup>\n"
        "</Project>\n"
    )


EXACT_VERSION = package_version("3.3.2")
COMPLETE_EDITORCONFIG = "root = true\n\n[*.cs]\nindent_size = 4\n"


def write_packages(root: Path, entries: str) -> None:
    """The repository's Directory.Packages.props, holding `entries`."""
    packages_file = root / "Directory.Packages.props"
    packages_file.write_text(packages_text(entries), encoding="utf-8")


def write_editorconfig(root: Path, text: str) -> None:
    """The repository's .editorconfig, holding `text`."""
    (root / ".editorconfig").write_text(text, encoding="utf-8")
