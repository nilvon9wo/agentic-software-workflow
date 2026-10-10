# Fix references to the renamed spec file and gate documentation file references

## Summary

Specification files were renamed from `spec/<key>.md` to
`spec/<key>-<title-slug>.md` (see `SpecificationFileName`), but
`docs/use/getting-started.md` still says the specifier writes `spec/8.md`.
Lychee does not catch this because the reference is inline code, not a
link. This item corrects the stale reference and adds a static gate,
`docrefs`, that fails when a Markdown file names, in inline code, a
repository file that does not exist.

## Acceptance criteria

- **AC-1**: Given `docs/use/getting-started.md`, when it is read, then it
  contains no `spec/8.md` and the step that describes the specifier writing
  a specification names the file as `spec/8-<title-slug>.md` (the issue
  number, a hyphen, the title slug), so it matches the real naming.
- **AC-2**: Given a checked Markdown file whose inline code (single
  backticks) holds a repository-relative path, such as `scripts/gates.sh`,
  that exists, when the `docrefs` gate runs, then it reports no finding for
  it.
- **AC-3**: Given such a reference to a path that does not exist, such as
  `spec/8.md`, when the gate runs, then the gate fails, and exactly one
  `Finding` is reported for it, with tool `docrefs`, rule `missing-file`,
  the Markdown file's name, and the line number of the reference.
- **AC-4**: Given a reference whose text is not a plain path, that is, it
  contains whitespace, `<`, `>`, `*`, `{`, `}`, `$`, `:` or `//`, such as
  `spec/8-<title-slug>.md` or `https://example.com/a.md`, when the gate
  runs, then it is ignored.
- **AC-5**: Given inline code with no `/` in it, such as `Program.cs` or
  `CODEOWNERS`, when the gate runs, then it is ignored (only paths with a
  directory part are checked).
- **AC-6**: Given a path inside a fenced code block, when the gate runs,
  then it is ignored; only inline code spans are checked.
- **AC-7**: Given a reference ending in `/` to a directory that exists, such
  as `spec/`, when the gate runs, then it passes; given one to a directory
  that does not exist, then it fails as in AC-3.
- **AC-8**: Given a reference in a Markdown file, when it is resolved, then
  it is resolved relative to the repository root (not to the Markdown
  file's directory), so `docs/vision.md` is checked as that path from the
  root.
- **AC-9**: Given a failing run, when the gate's output is read, then it
  lists each missing path with its file and line, so the author can find it.
- **AC-10**: Given `scripts/gates/registry.py`, when the static gates are
  listed, then `docrefs` is among them and runs as part of
  `scripts/gates.sh`.
- **AC-11**: Given `scripts/gates.sh verify`, when it runs, then a canary
  Markdown file in `tests/StyleCanary` that references a nonexistent file
  makes the `docrefs` gate report a finding, and the verification fails if
  it does not.
- **AC-12**: Given the repository as committed, when `scripts/gates.sh`
  runs, then `docrefs` passes, and the Python gate code has 100% line and
  branch coverage with tests for AC-2 to AC-9.
- **AC-13**: Given the gate's documentation in
  `docs/how-it-works/quality-gates.md`, when it is read, then it describes
  `docrefs` alongside the other Markdown gates.

## Out of scope

- Changing `lychee`, which keeps checking real links.
- Checking Markdown links, anchors, or remote URLs.
- Checking references in code comments, C# or Python sources, or YAML.
- Checking files under `spec/`, which may legitimately name files that the
  work will create.
- Changing the synthetic `spec/8.md` paths in
  `tests/scripts/test_maintainer_approval.py` and the `"8.md"` result in
  `SpecificationFileNameTest`. The first are made-up pull-request file
  lists, and the second is the intended output when a title has no letters
  or digits.

## Decisions

- The only stale reference in documentation is `getting-started.md`; the
  other `8.md` matches are test fixtures (see Out of scope).
- The documentation text uses a placeholder, `spec/8-<title-slug>.md`,
  rather than a real file name, since the name depends on the title.
- The gate is a new custom Python gate, because no established tool checks
  inline-code paths; lychee only follows links.
- Only inline code with a `/` is checked, to avoid false positives on
  names, commands, and bare file names.
- Paths resolve from the repository root, since the documentation writes
  them that way.
- `spec/` is excluded from checking, because specifications describe
  future files.
- The gate is named `docrefs`, with rule `missing-file`, following the
  existing `tool`/`rule` finding convention.
