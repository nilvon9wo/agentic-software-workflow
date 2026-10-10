# Resolve conflicts between open proposals without a human

## Summary

When one proposal merges, other open proposals can conflict with `master`.
Today `IChangeProposing.UpdateBehind` runs `gh pr update-branch` on proposals
that are behind and, when that fails, only logs "Could not bring … up to
date"; the proposal then waits for a human. This item (1) stops new builds
from starting while another build pull request is open, so fewer conflicts
arise, (2) resolves the conflicts that remain by re-running the specifier
(for a specification proposal) or the build (for a build proposal) from
current `master` on a fresh branch, and closing the old pull request with a
link, and (3) escalates to a maintainer, with the conflicting files, when
that one automatic attempt does not succeed.

A proposal is **in conflict** when GitHub reports its `mergeStateStatus` as
`DIRTY`. A proposal that is `BEHIND` is still handled by `UpdateBehind`,
unchanged. A proposal whose state is anything else (including `UNKNOWN`) is
left alone for this pass.

## Acceptance criteria

### Serial builds

- **AC-1** Given an open pull request of kind `ProposalKind.Implementation`
  and an item whose specification has merged into `master`, when
  `BuildCommand.ListBuildable` runs, then it succeeds with an empty list.
- **AC-2** Given no open pull request of kind `ProposalKind.Implementation`
  and two items with merged specifications, when `BuildCommand.ListBuildable`
  runs, then it returns both, exactly as it does today.
- **AC-3** Given an open pull request of kind `ProposalKind.Specification`
  only, when `BuildCommand.ListBuildable` runs, then builds are not blocked
  (only the existing exclusion of the same item's open specification
  applies).
- **AC-4** Given `IChangeProposing` cannot list the open build proposals,
  when `BuildCommand.ListBuildable` runs, then it returns that failure and
  starts nothing; `RunLoop` logs "Could not list the items ready to build:"
  followed by the failure message.
- **AC-5** Given a build proposal is closed or merged between two passes,
  when the next pass lists buildable items, then building resumes.

### Detecting conflicts

- **AC-6** Given open proposals whose `mergeStateStatus` is `DIRTY`,
  `BEHIND`, `CLEAN`, and `UNKNOWN`, when
  `IChangeProposing.ListConflicted` runs, then it returns only the `DIRTY`
  one, as a `ConflictedProposal` holding the work item, the
  `ProposalKind` read from the proposal's marker, the pull request address,
  its head branch, and the conflicting file paths.
- **AC-7** Given a `DIRTY` proposal, when its conflicting files are
  determined, then they are the paths that conflict when its head is merged
  with `master`, in ordinal order, each path once.
- **AC-8** Given the conflicting files cannot be determined, when the
  proposal is listed, then the file list is empty and listing still
  succeeds.
- **AC-9** Given the open proposals cannot be listed, when
  `ListConflicted` runs, then it returns that failure.

### Specification proposals in conflict

- **AC-10** Given a `DIRTY` specification proposal for an item that is not
  waiting on a human (`WorkItem.IsWaiting` is false), when `RunLoop` makes a
  pass, then the specifier is run for that item in a new workspace created
  from `master` on the branch `aswf/specify-<SafeKey>-replaces-<N>`, where
  `<N>` is the old pull request number.
- **AC-11** Given the specifier is run under AC-10, when it is briefed, then
  the brief includes every review, review comment, and line comment by a
  maintainer on the old pull request, not only those since its last commit;
  comments by anyone else are left out, as everywhere else.
- **AC-12** Given the specifier produces a specification under AC-10, when
  it is proposed, then a new pull request is created whose body names the
  old pull request's address, and then the old pull request receives a
  comment containing the new address and is closed.
- **AC-13** Given AC-12, when the new pull request is created, then it is
  still a `ProposalKind.Specification` proposal for the same item, so
  `ReadReview` and `ListAwaitingRevision` find it, and the old one is no
  longer open.
- **AC-14** Given the old pull request could not be closed, when AC-12
  runs, then the failure is reported as for any other failure (AC-19) and
  the new pull request is left open.
- **AC-15** Given the specifier asks questions instead of producing a
  specification, when AC-10 runs, then no new pull request is created, the
  old one stays open, and the item waits on a human by the specifier's
  existing path.

### Build proposals in conflict

- **AC-16** Given a `DIRTY` build proposal for an item that is not waiting
  on a human, when `RunLoop` makes a pass, then the full build runs for that
  item in a new workspace created from `master` on the branch
  `aswf/build-<SafeKey>-replaces-<N>`, whether or not the item is currently
  returned by `ListBuildable`.
- **AC-17** Given the rebuild under AC-16 succeeds, when it is proposed,
  then a new pull request is created whose body names the old pull request
  and carries `Closes #<key>` as for any build, auto-merge is requested on
  it, the old pull request receives a comment containing the new address and
  is closed, and the item stays marked built.
- **AC-18** Given a build proposal is in conflict, when a rebuild is
  started, then it is not blocked by the old pull request being open (AC-1
  does not apply to the rebuild of that same proposal).

### Failing and escalating

- **AC-19** Given the automatic attempt under AC-10 or AC-16 fails for any
  reason (workspace, specifier, build stage, gates, push, proposing, or
  closing), when the failure is handled, then `IWorkSupplying.Ask` is called
  for the item with a message that contains the old pull request address,
  the failure message, and each conflicting file path on its own line (or
  the sentence "The conflicting files could not be determined." when there
  are none), and `RunLoop` logs "Asked a maintainer about <item>.".
- **AC-20** Given a `DIRTY` proposal whose item is waiting on a human, when
  `RunLoop` makes a pass, then no workspace is created and nothing is run
  for it: one automatic attempt only.
- **AC-21** Given a maintainer has replied and the item is no longer
  waiting, when the next pass finds the proposal still `DIRTY`, then one
  further automatic attempt is made.
- **AC-22** Given an attempt hits a usage limit (`UsageLimitReached`), when
  it is handled, then it is waited out and retried exactly as other jobs
  are, and the maintainer is not asked.
- **AC-23** Given one conflicted proposal fails, when the same pass has
  other conflicted proposals, then each of the others is still attempted.

### Order within a pass

- **AC-24** Given a pass, when it runs, then it first calls
  `UpdateBehindProposals`, then resolves conflicted proposals, then lists
  and revises proposals awaiting revision, then processes ready items, then
  builds buildable items.
- **AC-25** Given conflicted proposals cannot be listed, when a pass runs,
  then "Could not list the conflicted proposals: <message>" is logged and the
  rest of the pass still runs.

## Out of scope

- Allowing a new build while a build pull request is open when their files
  do not overlap (see Decisions).
- Reusing the old build's tests (see Decisions).
- Keeping a maintainer's approval when an approved specification only needs
  updating without conflict. That is #75; this item does not change
  `scripts/maintainer_approval.py` or how approvals bind to commits.
- Resolving conflicts by merging or editing files by hand, or by any
  worker other than the specifier and the build stage.
- Changing how `BEHIND` proposals are updated.
- Proposals not authored by the workflow's own account (the list is
  already limited to `--author @me`, 30 pull requests).

## Decisions

- **Strict serial builds.** The issue allows a new build when its
  specification "touches none of" the open build's files. Specifications
  have no machine-readable list of files, so overlap cannot be decided
  reliably. No new build starts while any build pull request is open. With
  auto-merge, open builds are short-lived, as the issue notes.
- **Full rebuild only.** The issue allows reusing tests "if they still
  apply", but there is no deterministic test for that. A conflicted build is
  always rebuilt in full; reuse can be added later.
- **Conflict means `DIRTY`.** It is the state GitHub reports for a
  pull request that cannot merge cleanly, so no `update-branch` failure
  text is parsed. `UNKNOWN` is transient and is retried on the next pass.
- **One automatic attempt** is enforced by the `needs-human` label: while
  the item waits on a human, a still-conflicted proposal is skipped. The
  maintainer's reply clears the label and permits one more attempt.
- **Branch names** use `-replaces-<N>` with the old pull request number,
  so a fresh branch never collides with the old one, which stays open until
  the new pull request exists, and names do not grow across repeated
  conflicts.
- **Old pull request is closed only after the new one exists**, so a failed
  attempt never leaves the item with no open proposal.
- **A re-run specification needs a new approval.** It is a new commit on a
  new pull request, and approvals bind to the head commit; only #75 can
  change that for pull requests that merely needed updating.
- **Escalation goes on the work item** (`IWorkSupplying.Ask`), where
  `needs-human` already lives, with the old pull request address in the
  message.
