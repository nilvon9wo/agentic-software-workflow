# Document why each non-C# language is used, and gate new ones

## Summary

The repository is meant to be primarily C#, but its Python, shell, YAML and
Node tooling is nowhere explained, so the language choices look arbitrary.
This item adds a "Languages" section to
`docs/how-it-works/architecture.md` that states the rule (C# unless a tool
or other reason justifies otherwise), lists the criteria that justify an
exception, and records every non-C# language the repository uses, where it
lives, and why C# was not used. It also adds a deterministic gate,
`languages`, so that a source file in an undocumented language cannot be
added without a maintainer-visible documentation change. The gate checks
that every tracked programming-language file outside C# is covered by a row
of the table and that every row carries a justification. It does not judge
whether a justification is good; that remains a review matter.

## Acceptance criteria

### Documentation

- **AC-1**: Given `docs/how-it-works/architecture.md`, when it is read, then
  it contains a second-level heading `## Languages` whose text states that
  C# is the project's main language and that any other language is an
  exception that must be justified and listed.
- **AC-2**: Given the `## Languages` section, when it is read, then it names
  these justification criteria: existence or availability of tools,
  accuracy or precision, speed or performance, stability, maintainability,
  and ease of use.
- **AC-3**: Given the `## Languages` section, when it is read, then it
  contains one Markdown table whose header row is exactly
  `| Language | Files | Why not C# |`.
- **AC-4**: Given the table, when it is read, then it has a row for each of
  Python, Shell, YAML (GitHub Actions workflows) and Node (the private
  runtime that `scripts/gates.sh` installs for markdownlint), and each
  row's `Why not C#` cell names the specific tool, constraint or criterion
  that justifies the exception rather than a generic phrase.
- **AC-5**: Given a table row, when its `Files` cell is read, then it holds
  one or more git pathspec globs, each in backticks and separated by
  commas, for example `` `scripts/**/*.py`, `tests/scripts/**/*.py` ``.
- **AC-6**: Given the section, when it is read, then the C# files are not a
  row of the table, since C# is the rule and not an exception.
- **AC-7**: Given `docs/how-it-works/quality-gates.md`, when its gates table
  is read, then it has a `languages` row naming the gate and what it
  catches, and `docs/how-it-works/architecture.md` links to it from the
  `## Languages` section.

### The gate

The gate is `languages`, implemented in `scripts/gates/language_gate.py` as
`run_languages(target: Target) -> GateResult`, registered in
`STATIC_GATES` in `scripts/gates/registry.py`. The logic is a pure function
`check_languages(table: str, files: Sequence[str]) -> list[Finding]` where
`table` is the text of the `## Languages` section and `files` are
repository-relative paths with `/` separators. `run_languages` reads the
section from `docs/how-it-works/architecture.md` and lists files with
`repository_files`.

A "language file" is a file with one of these extensions: `.py`, `.sh`,
`.bash`, `.ps1`, `.psm1`, `.bat`, `.cmd`, `.js`, `.mjs`, `.cjs`, `.ts`,
`.rb`, `.go`, `.java`, `.rs`, `.pl`. Files with other extensions (`.cs`,
`.md`, `.json`, `.jsonc`, `.yml`, `.yaml`, `.toml`, `.xml`, `.csproj`,
`.props`, `.txt`, and so on) are configuration, data or documentation and
are never reported as undocumented languages.

- **AC-8**: Given a table row whose `Files` globs match every language file
  in `files` and whose `Why not C#` cell is non-empty, when
  `check_languages` runs, then it returns no findings.
- **AC-9**: Given a language file in `files` that no `Files` glob matches,
  when `check_languages` runs, then it returns a `Finding` with gate
  `languages`, rule `undocumented-language`, that file's path as
  `file_name`, and `WHOLE_FILE` as `line_number`.
- **AC-10**: Given several unmatched language files, when `check_languages`
  runs, then it returns one `Finding` per file.
- **AC-11**: Given a table row whose `Why not C#` cell is empty or only
  whitespace, when `check_languages` runs, then it returns a `Finding` with
  rule `missing-justification` and `file_name`
  `docs/how-it-works/architecture.md`, even if every file is matched.
- **AC-12**: Given a table row whose `Files` cell contains no backticked
  glob, when `check_languages` runs, then it returns a `Finding` with rule
  `missing-files`.
- **AC-13**: Given a `table` with no row below the header, or text with no
  table headed `| Language | Files | Why not C# |`, when `check_languages`
  runs, then it returns a `Finding` with rule `missing-table`, whatever
  `files` holds.
- **AC-14**: Given a `.cs` file, a `.md` file or a `.yml` file in `files`
  that no glob matches, when `check_languages` runs, then it returns no
  finding for it.
- **AC-15**: Given a glob such as `scripts/**/*.py`, when matching
  `scripts/gates/model.py` and `scripts/run_gates.py`, then both match, and
  `tests/scripts/test_x.py` does not; a `**` spans any number of
  directories, including none.
- **AC-16**: Given `docs/how-it-works/architecture.md` cannot be read or
  has no `## Languages` heading, when `run_languages` runs, then the
  result has exit code 1 and a `missing-table` finding rather than
  passing.
- **AC-17**: Given the real repository, when `scripts/gates.sh run
  languages` runs, then it passes: every tracked language file, including
  those under `tests/StyleCanary/`, is matched by a table row.
- **AC-18**: Given the canary under `tests/StyleCanary`, when
  `scripts/gates.sh verify` runs, then a violation tagged
  `expect: languages:undocumented-language` is reported by the gate and
  `verify` fails if it is not. The canary file used is excluded from the
  real-repository check by being matched in the table, as the existing
  canary files are.
- **AC-19**: Given the `languages` gate, when `scripts/run_gates.py` lists
  `ALL_GATES`, then `languages` appears among the static gates, and
  `scripts/gates.sh` with no arguments runs it.

## Out of scope

- Porting any existing Python, shell or YAML to C#. The item documents and
  guards the current state; migrations are separate work.
- Judging whether a justification is convincing, or measuring the share of
  C# against other languages by lines or files. No percentage threshold.
- Gating configuration and data formats (JSON, TOML, YAML contents,
  MSBuild files, Markdown) or file types other than the extensions listed.
- Languages inside Markdown code blocks and inline shell in workflow
  `run:` steps; those are already covered by `markdownlint` and
  `actionlint`.
- Changing any other gate's behaviour.

## Decisions

- **A gate is added.** The item was unsure. A deterministic check costs no
  tokens and cannot be argued with, which suits the stated aim of holding
  the project to C#. It enforces documentation, not language choice, so a
  new language still needs a maintainer to accept the change.
- **The documentation table is the allow-list.** A separate data file
  would duplicate the table and drift from it. The gate parses the table,
  so the justification and the permission are the same text.
- **Only listed extensions count as languages.** Configuration and data
  formats are not programming languages in the item's sense; including
  them would flag every `.json` file. The list is a constant that a
  maintainer can extend.
- **YAML and Node are documented but only partly gated.** `.yml` is not in
  the extension list, but workflows are still a technology on the stack,
  so they get a row. Node is documented because `gates.sh` installs it.
- **Canary and test files are listed in the table.** They are real Python
  and shell files in the repository, so they need rows or globs like any
  other.
- **Finding rule names** (`undocumented-language`, `missing-justification`,
  `missing-files`, `missing-table`) are chosen here so tests can assert
  them.
- **The gate goes in `STATIC_GATES`**, since it reads files and documents
  without running code, and `docs/how-it-works/quality-gates.md` gains its
  row as the existing gates have.
- **Changes to `.github/` and `scripts/` need a maintainer's approval**
  under `CLAUDE.md` if `ci.yml` or the verify tooling is touched; the gate
  itself is reached through `gates.sh`, so no workflow change is expected.
