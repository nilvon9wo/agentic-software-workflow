# Intent-carrying git branch names

## Summary

The conductor names the branches it creates `aswf/specify-<key>` and
`aswf/build-<key>`, which say nothing about what the change is for. Every
branch the workflow creates, and every branch an interactive session creates
in this repository, is instead named `<type>/<ref>/<intent-slug>`, for example
`bug/GHI-ASW-28/useless-record-decomposition-tests`. The conductor computes
the name from the work item in a new `BranchName` type (namespace
`AgenticSoftwareWorkflow.Conductor.Work`), and `CLAUDE.md` states the rule for
interactive sessions.

## Acceptance criteria

A `WorkItem` below is the existing record (`Id`, `Title`, `Body`, `Labels`,
`Comments`, `IsWaiting`). `BranchName.For(WorkItem item, ProposalKind kind)`
returns the branch name as a `string`.

### Type segment

- **AC-1**: Given an item labelled `bug` and `kind` of
  `ProposalKind.Implementation`, when `BranchName.For` runs, then the name
  starts with `bug/`.
- **AC-2**: Given an item labelled `enhancement` and `kind` of
  `ProposalKind.Implementation`, when `BranchName.For` runs, then the name
  starts with `enhancement/`.
- **AC-3**: Given an item with neither label (for example only `question`, or
  no labels) and `kind` of `ProposalKind.Implementation`, when
  `BranchName.For` runs, then the name starts with `task/`.
- **AC-4**: Given an item labelled both `bug` and `enhancement` and `kind` of
  `ProposalKind.Implementation`, when `BranchName.For` runs, then the name
  starts with `bug/`.
- **AC-5**: Given an item with any labels, including `bug` or `enhancement`,
  and `kind` of `ProposalKind.Specification`, when `BranchName.For` runs, then
  the name starts with `spec/`.
- **AC-6**: Given the same item, when `BranchName.For` runs once with
  `ProposalKind.Specification` and once with `ProposalKind.Implementation`,
  then the two names differ.

### Reference segment

- **AC-7**: Given an item whose `Id` is
  `WorkItemId("github:nilvon9wo/agentic-software-workflow", "60")`, when
  `BranchName.For` runs, then the second segment is `GHI-ASW-60`.
- **AC-8**: Given an item whose `Id` source is `github:acme/billing-service`
  and key `7`, when `BranchName.For` runs, then the second segment is
  `GHI-BS-7`. The abbreviation is the upper-cased first letter of each
  hyphen-separated word of the repository name.
- **AC-9**: Given an item whose `Id` source does not start with `github:`
  (for example `jira:acme`) and key `PROJ-12`, when `BranchName.For` runs,
  then the second segment is `PROJ-12`.
- **AC-10**: Given an item whose non-GitHub key contains a character a file or
  branch name cannot hold, such as `/`, when `BranchName.For` runs, then the
  reference segment is `WorkItemId.SafeKey`, so the name always has exactly
  three `/`-separated segments.

### Intent slug

- **AC-11**: Given an item titled `Useless record decomposition tests`, when
  `BranchName.For` runs, then the third segment is
  `useless-record-decomposition-tests`.
- **AC-12**: Given a title with more than six words, such as
  `Most of the branch names are garbage`, when `BranchName.For` runs, then the
  slug holds only the first six words: `most-of-the-branch-names-are`.
- **AC-13**: Given a title with capitals, punctuation, or repeated
  separators, such as `HLQ005: shows in Visual Studio -- as an error!`, when
  `BranchName.For` runs, then the slug is lower case, made only of ASCII
  letters, digits, and single hyphens, and neither starts nor ends with a
  hyphen (`hlq005-shows-in-visual-studio-as`).
- **AC-14**: Given a title with no ASCII letters or digits (empty, or only
  symbols or non-Latin text), when `BranchName.For` runs, then the slug is
  `change`.

### Use by the conductor

- **AC-15**: Given the conductor specifies an item titled
  `Most of the branch names are garbage`, labelled `enhancement`, id
  `github:nilvon9wo/agentic-software-workflow#60`, when
  `SpecifyCommand.Run` runs, then the workspace is created on branch
  `spec/GHI-ASW-60/most-of-the-branch-names-are`, from the base branch, and
  the pull request is proposed from that branch.
- **AC-16**: Given the conductor builds the same item, when
  `BuildCommand.Build` runs, then the workspace is created on branch
  `enhancement/GHI-ASW-60/most-of-the-branch-names-are`, from the base
  branch, and the pull request is proposed from that branch.
- **AC-17**: Given the work item cannot be read, when `SpecifyCommand.Run`
  runs, then it returns that failure and creates no workspace and no branch.
  (`BuildCommand.Build` already reads the item inside the workspace; it now
  reads it first and behaves the same way.)
- **AC-18**: Given a specification pull request already open on a branch
  named `aswf/specify-60` (created before this change), when
  `SpecifyCommand.Revise` runs, then it opens a workspace on that existing
  branch, taken from `ProposalReview.Branch`, and does not rename it.
- **AC-19**: Given a branch name containing `/`, when
  `GitRepository.CreateWorkspace` runs, then the workspace directory is still
  a single directory under `.aswf/worktrees/` whose name replaces each `/`
  with `-` (existing behaviour, kept).
- **AC-20**: Given a pull request is matched to its work item, when the
  conductor lists or reads reviews, then it matches by the hidden body marker
  as before, never by branch name, so branches of either naming scheme are
  found.

### Interactive sessions

- **AC-21**: Given `CLAUDE.md`, when it is read, then its *Working rules*
  section states that every branch is named `<type>/<ref>/<intent-slug>`,
  with `type` one of `bug`, `enhancement`, `task`, or `spec`, `ref` the work
  item reference such as `GHI-ASW-<number>`, and `intent-slug` a few words
  saying what the change is for, and gives at least one example.

## Out of scope

- Renaming or deleting branches that already exist, open or merged.
- An extra segment for a component or area, such as
  `enhancement/GHI-ASW-60/conductor/...`. Branches, pull requests, and issues
  can already be labelled, so the fixed three-segment form is kept.
- Choosing a slug with judgement: the conductor derives it mechanically from
  the title (see *Decisions*). A hand-written slug remains the rule for
  interactive sessions.
- Enforcing the rule on branches pushed by hand (no hook, check, or branch
  protection).
- Handling a branch name that already exists on the remote; failure then
  surfaces as `GitRepository.CreateWorkspace` fails today.

## Decisions

- **Slug is derived mechanically.** The issue asks for an intent slug rather
  than the bare title, but the conductor must name the branch before any role
  runs, and a model-written slug would add a cost and a failure mode to every
  run. The first six words of the title are the default. If a maintainer
  wants better slugs, the specifier or a later stage could supply one.
- **Six words, ASCII only.** Keeps names short and valid in git and on every
  file system. The fallback `change` stops an empty segment.
- **`bug` wins over `enhancement`** when both labels are present, as the more
  urgent kind of change.
- **Reference abbreviation.** `GHI-ASW` in the examples is "GitHub Issue" plus
  the initials of `agentic-software-workflow`. The same rule is applied to any
  other GitHub repository, so no per-repository configuration is needed.
  Other trackers use the key itself, as the maintainers proposed.
- **No component segment.** The maintainer made it optional and conditional on
  cost; labels already carry that information.
- **The conductor reads the item before creating the workspace** (AC-17), as
  the name needs its title and labels. A failed read now creates nothing.
- **Existing branches are untouched** (AC-18), so open pull requests keep
  working.
- **Interactive sessions are covered by documentation only** (AC-21), as the
  issue asks for the rule to apply to them, but no tool creates their
  branches.
