"""Tests that a bare `await` builds clean under the suppressions (AC-9)."""

import re
from typing import TYPE_CHECKING

import pytest

from gates.tools import (
    REPOSITORY_ROOT,
    CompletedTool,
    combined_output,
    execute,
)

if TYPE_CHECKING:
    from pathlib import Path

PACKAGES_FILE = REPOSITORY_ROOT / "Directory.Packages.props"
SUPPRESSIONS_FILE = (
    REPOSITORY_ROOT
    / "src"
    / "AgenticSoftwareWorkflow.Cli"
    / "GlobalSuppressions.cs"
)
VERSION_PATTERN = re.compile(
    r'Include="ConfigureAwait\.Fody"\s+Version="([^"]+)"',
)
EDITORCONFIG = (
    "root = true\n\n"
    "[*.cs]\n"
    "dotnet_diagnostic.CA2007.severity = warning\n"
)
SOURCE = "await System.Threading.Tasks.Task.Delay(1);\n"


def project_with_version(version: str) -> str:
    """A warnings-as-errors project referencing ConfigureAwait.Fody."""
    return (
        '<Project Sdk="Microsoft.NET.Sdk">\n'
        "  <PropertyGroup>\n"
        "    <OutputType>Exe</OutputType>\n"
        "    <TargetFramework>net10.0</TargetFramework>\n"
        "    <Nullable>enable</Nullable>\n"
        "    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>\n"
        "  </PropertyGroup>\n"
        "  <ItemGroup>\n"
        '    <PackageReference Include="ConfigureAwait.Fody"'
        f' Version="{version}" PrivateAssets="all" />\n'
        "  </ItemGroup>\n"
        "</Project>\n"
    )


@pytest.fixture(name="build", scope="module")
def bare_await_build(tmp_path_factory: pytest.TempPathFactory) -> CompletedTool:
    """`dotnet build` of a throwaway project holding a bare `await`."""
    project_directory: Path = tmp_path_factory.mktemp("bare-await")
    packages_text = PACKAGES_FILE.read_text(encoding="utf-8")
    version_match = VERSION_PATTERN.search(packages_text)
    assert version_match is not None
    version = version_match.group(1)
    project = project_directory / "BareAwait.csproj"
    project.write_text(project_with_version(version), encoding="utf-8")
    (project_directory / "Program.cs").write_text(SOURCE, encoding="utf-8")
    (project_directory / ".editorconfig").write_text(
        EDITORCONFIG,
        encoding="utf-8",
    )
    suppressions = SUPPRESSIONS_FILE.read_text(encoding="utf-8")
    (project_directory / "GlobalSuppressions.cs").write_text(
        suppressions,
        encoding="utf-8",
    )
    return execute(["dotnet", "build", str(project), "--nologo"])


def test_build_when_an_await_has_no_configure_await_succeeds(
    build: CompletedTool,
) -> None:
    # Arrange
    # Nothing to arrange: the project is built by the fixture.

    # Act
    exit_code: int = build.returncode

    # Assert
    assert exit_code == 0, combined_output(build)


def test_build_when_an_await_has_no_configure_await_emits_no_ca2007(
    build: CompletedTool,
) -> None:
    # Arrange
    # Nothing to arrange: the project is built by the fixture.

    # Act
    output: str = combined_output(build)

    # Assert
    assert "CA2007" not in output


def test_build_when_an_await_has_no_configure_await_emits_no_enforcer(
    build: CompletedTool,
) -> None:
    # Arrange
    # Nothing to arrange: the project is built by the fixture.

    # Act
    output: str = combined_output(build)

    # Assert
    assert "ConfigureAwaitEnforcer" not in output
