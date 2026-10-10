"""Tests for scripts/gates/configure_await_gate.py.

Covers AC-1, AC-5, AC-11 and AC-12.
"""

from pathlib import Path

import pytest
from configure_await_projects import (
    COMPLETE_EDITORCONFIG,
    EMPTY_JUSTIFICATION,
    EXACT_VERSION,
    NOTHING,
    PACKAGE_REFERENCE,
    PENDING_JUSTIFICATION,
    REAL_JUSTIFICATION,
    UNPRIVATE_PACKAGE_REFERENCE,
    UNRELATED_JUSTIFICATION,
    enforcer_suppression,
    package_version,
    project_text,
    suppressions_text,
    usage_suppression,
    write_editorconfig,
    write_packages,
    write_project,
    write_project_without_file,
)
from gate_testing import a_target

from gates import configure_await_gate as gate
from gates.model import WHOLE_FILE, Finding, GateResult
from gates.registry import STATIC_GATES

ALPHA = "Alpha.csproj"
PACKAGES_FINDING = Finding(
    "configure-await",
    "package-version",
    "Directory.Packages.props",
    WHOLE_FILE,
)
EDITORCONFIG_FINDING = Finding(
    "configure-await",
    "editorconfig-suppression",
    ".editorconfig",
    WHOLE_FILE,
)


def finding_for(project: str, rule: str) -> Finding:
    """The Finding the gate reports for a project's missing part."""
    return Finding("configure-await", rule, project, WHOLE_FILE)


@pytest.fixture(name="repository_in", autouse=True)
def throwaway_repository(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> Path:
    """The gate reads its projects from a throwaway repository."""
    monkeypatch.setattr(gate, "REPOSITORY_ROOT", tmp_path)
    write_packages(tmp_path, EXACT_VERSION)
    write_editorconfig(tmp_path, COMPLETE_EDITORCONFIG)
    return tmp_path


def test_run_configure_await_when_every_project_is_complete_passes(
    repository_in: Path,
) -> None:
    # Arrange
    write_project(repository_in, "src/Alpha")
    write_project(repository_in, "tests/Beta.Test")

    # Act
    result: GateResult = gate.run_configure_await(a_target())

    # Assert
    assert result.has_passed


def test_run_configure_await_when_package_reference_is_missing_reports_it(
    repository_in: Path,
) -> None:
    # Arrange
    write_project(repository_in, "src/Alpha", package_reference=NOTHING)

    # Act
    result: GateResult = gate.run_configure_await(a_target())

    # Assert
    assert result.findings == [finding_for(ALPHA, "package-reference")]


def test_run_configure_await_when_reference_is_not_private_reports_it(
    repository_in: Path,
) -> None:
    # Arrange
    write_project(
        repository_in,
        "src/Alpha",
        package_reference=UNPRIVATE_PACKAGE_REFERENCE,
    )

    # Act
    result: GateResult = gate.run_configure_await(a_target())

    # Assert
    assert result.findings == [finding_for(ALPHA, "package-reference")]


def test_run_configure_await_when_reference_is_in_build_props_passes(
    repository_in: Path,
) -> None:
    # Arrange
    build_props = repository_in / "Directory.Build.props"
    build_props.write_text(project_text(PACKAGE_REFERENCE), encoding="utf-8")
    write_project(repository_in, "src/Alpha", package_reference=NOTHING)

    # Act
    result: GateResult = gate.run_configure_await(a_target())

    # Assert
    assert result.has_passed


def test_run_configure_await_when_suppressions_file_is_missing_reports_it(
    repository_in: Path,
) -> None:
    # Arrange
    write_project_without_file(repository_in, "src/Alpha")

    # Act
    result: GateResult = gate.run_configure_await(a_target())

    # Assert
    assert result.findings == [finding_for(ALPHA, "global-suppressions")]


def test_run_configure_await_when_attribute_is_missing_reports_it(
    repository_in: Path,
) -> None:
    # Arrange
    suppressions = suppressions_text(attribute=NOTHING)
    write_project(repository_in, "src/Alpha", suppressions=suppressions)

    # Act
    result: GateResult = gate.run_configure_await(a_target())

    # Assert
    assert result.findings == [finding_for(ALPHA, "fody-attribute")]


def test_run_configure_await_when_attribute_is_true_reports_it(
    repository_in: Path,
) -> None:
    # Arrange
    attribute = "[assembly: Fody.ConfigureAwait(true)]\n"
    suppressions = suppressions_text(attribute=attribute)
    write_project(repository_in, "src/Alpha", suppressions=suppressions)

    # Act
    result: GateResult = gate.run_configure_await(a_target())

    # Assert
    assert result.findings == [finding_for(ALPHA, "fody-attribute")]


def test_run_configure_await_when_enforcer_suppression_is_missing_reports_it(
    repository_in: Path,
) -> None:
    # Arrange
    suppressions = suppressions_text(enforcer=NOTHING)
    write_project(repository_in, "src/Alpha", suppressions=suppressions)

    # Act
    result: GateResult = gate.run_configure_await(a_target())

    # Assert
    assert result.findings == [finding_for(ALPHA, "suppress-enforcer")]


def test_run_configure_await_when_ca2007_suppression_is_missing_reports_it(
    repository_in: Path,
) -> None:
    # Arrange
    suppressions = suppressions_text(usage=NOTHING)
    write_project(repository_in, "src/Alpha", suppressions=suppressions)

    # Act
    result: GateResult = gate.run_configure_await(a_target())

    # Assert
    assert result.findings == [finding_for(ALPHA, "suppress-ca2007")]


def test_run_configure_await_when_enforcer_check_id_is_wrong_reports_it(
    repository_in: Path,
) -> None:
    # Arrange
    wrong = enforcer_suppression(
        REAL_JUSTIFICATION,
        check_id="ConfigureAwaitEnforcer",
    )
    suppressions = suppressions_text(enforcer=wrong)
    write_project(repository_in, "src/Alpha", suppressions=suppressions)

    # Act
    result: GateResult = gate.run_configure_await(a_target())

    # Assert
    assert result.findings == [finding_for(ALPHA, "suppress-enforcer")]


def test_run_configure_await_when_enforcer_category_is_wrong_reports_it(
    repository_in: Path,
) -> None:
    # Arrange
    wrong = enforcer_suppression(REAL_JUSTIFICATION, category="Usage")
    suppressions = suppressions_text(enforcer=wrong)
    write_project(repository_in, "src/Alpha", suppressions=suppressions)

    # Act
    result: GateResult = gate.run_configure_await(a_target())

    # Assert
    assert result.findings == [finding_for(ALPHA, "suppress-enforcer")]


def test_run_configure_await_when_ca2007_category_is_wrong_reports_it(
    repository_in: Path,
) -> None:
    # Arrange
    wrong = usage_suppression(REAL_JUSTIFICATION, category="Style")
    suppressions = suppressions_text(usage=wrong)
    write_project(repository_in, "src/Alpha", suppressions=suppressions)

    # Act
    result: GateResult = gate.run_configure_await(a_target())

    # Assert
    assert result.findings == [finding_for(ALPHA, "suppress-ca2007")]


def test_run_configure_await_when_justification_is_pending_reports_it(
    repository_in: Path,
) -> None:
    # Arrange
    pending = enforcer_suppression(PENDING_JUSTIFICATION)
    suppressions = suppressions_text(enforcer=pending)
    write_project(repository_in, "src/Alpha", suppressions=suppressions)

    # Act
    result: GateResult = gate.run_configure_await(a_target())

    # Assert
    assert result.findings == [finding_for(ALPHA, "justification")]


def test_run_configure_await_when_justification_is_empty_reports_it(
    repository_in: Path,
) -> None:
    # Arrange
    empty = usage_suppression(EMPTY_JUSTIFICATION)
    suppressions = suppressions_text(usage=empty)
    write_project(repository_in, "src/Alpha", suppressions=suppressions)

    # Act
    result: GateResult = gate.run_configure_await(a_target())

    # Assert
    assert result.findings == [finding_for(ALPHA, "justification")]


def test_run_configure_await_when_justification_ignores_fody_reports_it(
    repository_in: Path,
) -> None:
    # Arrange
    unrelated = usage_suppression(UNRELATED_JUSTIFICATION)
    suppressions = suppressions_text(usage=unrelated)
    write_project(repository_in, "src/Alpha", suppressions=suppressions)

    # Act
    result: GateResult = gate.run_configure_await(a_target())

    # Assert
    assert result.findings == [finding_for(ALPHA, "justification")]


def test_run_configure_await_when_a_test_project_is_incomplete_reports_it(
    repository_in: Path,
) -> None:
    # Arrange
    write_project(repository_in, "src/Alpha")
    write_project_without_file(repository_in, "tests/Beta.Test")

    # Act
    result: GateResult = gate.run_configure_await(a_target())

    # Assert
    assert result.findings == [
        finding_for("Beta.Test.csproj", "global-suppressions"),
    ]


def test_run_configure_await_when_the_canary_is_incomplete_ignores_it(
    repository_in: Path,
) -> None:
    # Arrange
    write_project_without_file(repository_in, "tests/StyleCanary")

    # Act
    result: GateResult = gate.run_configure_await(a_target())

    # Assert
    assert result.has_passed


def test_run_configure_await_when_run_on_the_canary_checks_its_projects(
    repository_in: Path,
) -> None:
    # Arrange
    canary_project = Path("tests/StyleCanary/StyleCanary.csproj")
    write_project_without_file(repository_in, "tests/StyleCanary/Incomplete")
    target = a_target(dotnet_project=canary_project)

    # Act
    result: GateResult = gate.run_configure_await(target)

    # Assert
    assert finding_for("Incomplete.csproj", "global-suppressions") in (
        result.findings
    )


def test_run_configure_await_when_projects_are_incomplete_reports_each(
    repository_in: Path,
) -> None:
    # Arrange
    write_project_without_file(repository_in, "src/Alpha")
    write_project(repository_in, "src/Beta", package_reference=NOTHING)

    # Act
    result: GateResult = gate.run_configure_await(a_target())

    # Assert
    assert sorted(result.findings, key=lambda found: found.file_name) == [
        finding_for(ALPHA, "global-suppressions"),
        finding_for("Beta.csproj", "package-reference"),
    ]


def test_run_configure_await_when_a_file_is_missing_output_names_it(
    repository_in: Path,
) -> None:
    # Arrange
    write_project_without_file(repository_in, "src/Alpha")

    # Act
    result: GateResult = gate.run_configure_await(a_target())

    # Assert
    assert "Alpha.csproj: global-suppressions" in result.output


def test_run_configure_await_when_an_attribute_is_missing_output_names_it(
    repository_in: Path,
) -> None:
    # Arrange
    suppressions = suppressions_text(attribute=NOTHING)
    write_project(repository_in, "src/Alpha", suppressions=suppressions)

    # Act
    result: GateResult = gate.run_configure_await(a_target())

    # Assert
    assert "Alpha.csproj: fody-attribute" in result.output


def test_run_configure_await_when_a_justification_is_weak_output_names_it(
    repository_in: Path,
) -> None:
    # Arrange
    pending = enforcer_suppression(PENDING_JUSTIFICATION)
    suppressions = suppressions_text(enforcer=pending)
    write_project(repository_in, "src/Alpha", suppressions=suppressions)

    # Act
    result: GateResult = gate.run_configure_await(a_target())

    # Assert
    assert "Alpha.csproj: justification" in result.output


def test_run_configure_await_when_package_reference_is_missing_output_names(
    repository_in: Path,
) -> None:
    # Arrange
    write_project(repository_in, "src/Alpha", package_reference=NOTHING)

    # Act
    result: GateResult = gate.run_configure_await(a_target())

    # Assert
    assert "Alpha.csproj: package-reference" in result.output


def test_run_configure_await_when_a_suppression_is_missing_output_names_it(
    repository_in: Path,
) -> None:
    # Arrange
    suppressions = suppressions_text(enforcer=NOTHING)
    write_project(repository_in, "src/Alpha", suppressions=suppressions)

    # Act
    result: GateResult = gate.run_configure_await(a_target())

    # Assert
    assert "Alpha.csproj: suppress-enforcer" in result.output


def test_run_configure_await_when_ca2007_suppression_is_missing_output_names(
    repository_in: Path,
) -> None:
    # Arrange
    suppressions = suppressions_text(usage=NOTHING)
    write_project(repository_in, "src/Alpha", suppressions=suppressions)

    # Act
    result: GateResult = gate.run_configure_await(a_target())

    # Assert
    assert "Alpha.csproj: suppress-ca2007" in result.output


def test_run_configure_await_when_other_packages_are_pinned_passes(
    repository_in: Path,
) -> None:
    # Arrange
    other = package_version("1.2.3", "Other.Package")
    write_packages(repository_in, other + EXACT_VERSION)

    # Act
    result: GateResult = gate.run_configure_await(a_target())

    # Assert
    assert result.has_passed


def test_run_configure_await_when_the_version_is_not_pinned_reports_it(
    repository_in: Path,
) -> None:
    # Arrange
    write_packages(repository_in, package_version("1.0.0", "Other.Package"))

    # Act
    result: GateResult = gate.run_configure_await(a_target())

    # Assert
    assert result.findings == [PACKAGES_FINDING]


def test_run_configure_await_when_the_version_is_pinned_twice_reports_it(
    repository_in: Path,
) -> None:
    # Arrange
    entries = package_version("3.3.2") + package_version("3.3.1")
    write_packages(repository_in, entries)

    # Act
    result: GateResult = gate.run_configure_await(a_target())

    # Assert
    assert result.findings == [PACKAGES_FINDING]


def test_run_configure_await_when_the_version_floats_reports_it(
    repository_in: Path,
) -> None:
    # Arrange
    write_packages(repository_in, package_version("3.*"))

    # Act
    result: GateResult = gate.run_configure_await(a_target())

    # Assert
    assert result.findings == [PACKAGES_FINDING]


def test_run_configure_await_when_the_version_is_a_range_reports_it(
    repository_in: Path,
) -> None:
    # Arrange
    write_packages(repository_in, package_version("[3.0.0,4.0.0)"))

    # Act
    result: GateResult = gate.run_configure_await(a_target())

    # Assert
    assert result.findings == [PACKAGES_FINDING]


def test_run_configure_await_when_the_version_is_unpinned_output_names_it(
    repository_in: Path,
) -> None:
    # Arrange
    write_packages(repository_in, package_version("3.*"))

    # Act
    result: GateResult = gate.run_configure_await(a_target())

    # Assert
    assert "Directory.Packages.props: package-version" in result.output


def test_run_configure_await_when_editorconfig_sets_the_enforcer_reports_it(
    repository_in: Path,
) -> None:
    # Arrange
    key = "dotnet_diagnostic.ConfigureAwaitEnforcer.severity = none\n"
    write_editorconfig(repository_in, COMPLETE_EDITORCONFIG + key)

    # Act
    result: GateResult = gate.run_configure_await(a_target())

    # Assert
    assert result.findings == [EDITORCONFIG_FINDING]


def test_run_configure_await_when_editorconfig_sets_ca2007_reports_it(
    repository_in: Path,
) -> None:
    # Arrange
    key = "dotnet_diagnostic.CA2007.severity = none\n"
    write_editorconfig(repository_in, COMPLETE_EDITORCONFIG + key)

    # Act
    result: GateResult = gate.run_configure_await(a_target())

    # Assert
    assert result.findings == [EDITORCONFIG_FINDING]


def test_run_configure_await_when_editorconfig_sets_another_rule_passes(
    repository_in: Path,
) -> None:
    # Arrange
    key = "dotnet_diagnostic.CA1707.severity = none\n"
    write_editorconfig(repository_in, COMPLETE_EDITORCONFIG + key)

    # Act
    result: GateResult = gate.run_configure_await(a_target())

    # Assert
    assert result.has_passed


def test_run_configure_await_when_editorconfig_sets_a_rule_output_names_it(
    repository_in: Path,
) -> None:
    # Arrange
    key = "dotnet_diagnostic.CA2007.severity = none\n"
    write_editorconfig(repository_in, COMPLETE_EDITORCONFIG + key)

    # Act
    result: GateResult = gate.run_configure_await(a_target())

    # Assert
    assert ".editorconfig: editorconfig-suppression" in result.output


def test_static_gates_when_listed_include_the_configure_await_gate() -> None:
    # Arrange
    # Nothing to arrange: the gate registry is static.

    # Act
    names: list[str] = [registered.name for registered in STATIC_GATES]

    # Assert
    assert "configure-await" in names
