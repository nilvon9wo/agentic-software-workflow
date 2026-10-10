# Maintainer feedback on a build pull request stops its merge and revises it

## Summary

Build pull requests request auto-merge and merge by themselves once the checks
pass (PR #94). The repository requires no reviews, so a maintainer's comment or
change request on a build pull request does not stop that merge, and nothing
answers it. This item makes a maintainer's feedback on an open build pull
request (a pull request carrying the `<!-- aswf-build: <id> -->` marker) do
what it already does on a specification pull request: the conductor disables
the build's auto-merge as soon as it sees the feedback, hands the feedback to
the implementer as findings, pushes the revision to the same branch, re-requests
auto-merge, and replies on the pull request. Until the revision is pushed the
build cannot merge by itself.

## Acceptance criteria

Detection and stopping:

- **AC-1**: Given an open build pull request (build marker in its body) and a
  maintainer's change request, review comment, or pull-request comment made
  after the latest non-maintainer, non-merge commit, and no later maintainer
  approval, when `PullRequestFeedback.AwaitsRevision` is evaluated then it is
  true, exactly as for a specification pull request (same rules for approval,
  base-branch merge commits, maintainers' own commits, and non-maintainer
  authors).
- **AC-2**: Given open pull requests of both kinds awaiting revision, when
  `IChangeProposing.ListAwaitingRevision` runs then it returns the work item of
  each, specification and build alike, and no item twice.
- **AC-3**: Given an open build pull request awaiting revision, when
  `IChangeProposing` is asked to hold it (a new method, called
  `HoldForRevision`, taking a `CancellationToken` and returning
  `Fin<IReadOnlyList<string>>` of one report per pull request) then
  `GitHubPullRequests` runs `gh pr merge <url> --disable-auto` for that pull
  request.
- **AC-4**: Given open pull requests that are not awaiting revision (no
  feedback since the latest commit, or approved by a maintainer), or that are
  specification pull requests, when `HoldForRevision` runs then no
  `--disable-auto` command is run for them.
- **AC-5**: Given `--disable-auto` fails for one build pull request, when
  `HoldForRevision` runs then the report for that pull request names it and
  carries the failure message, the remaining pull requests are still held, and
  the result is a success. If the open pull requests cannot be listed, the
  result is that failure.
- **AC-6**: Given a run-loop pass, when it starts then it calls
  `IWorkProcessing.HoldForRevision` (delegating to `IChangeProposing`) before
  updating behind proposals, listing revisions, or building, and logs each
  report. If it fails, the pass logs
  `Could not hold the builds awaiting revision: <message>` and carries on.

Reading the review:

- **AC-7**: Given an open build pull request, when
  `IChangeProposing.ReadReview(id)` runs then the `ProposalReview` returned
  has `Kind == ProposalKind.Implementation` (a new property), the pull
  request's address and branch, and every maintainer review, comment, and line
  comment since the latest qualifying commit, oldest first, as for a
  specification.
- **AC-8**: Given an open specification pull request, when `ReadReview(id)`
  runs then `Kind == ProposalKind.Specification`.
- **AC-9**: Given an item with an open specification pull request and an open
  build pull request, when `ReadReview(id)` runs then the specification's is
  returned.
- **AC-10**: Given an item with no open pull request of either kind, when
  `ReadReview(id)` runs then it fails with `ProposalNotFound` for that item,
  as today.
- **AC-11**: Given an open build pull request for an item and no open
  specification pull request, when `ListOpenSpecifications` runs then the item
  is not returned: a build under review never blocks, or counts as, a
  specification being revised.

Revising:

- **AC-12**: Given `IWorkProcessing.Revise(id)` and the item's review is a
  specification, when it runs then the specification is revised exactly as
  before this item.
- **AC-13**: Given the review is a build, when `Revise(id)` runs then
  `BuildCommand.Revise(id)` runs: the review's branch is opened as a workspace,
  the implementer runs there with a brief containing the work item, the
  approved specification (read from the workspace's `spec/` file for the item),
  and a heading `What to fix` followed by each piece of feedback as
  `### <author> reviewed` and its body, oldest first. The test author and test
  reviewer do not run.
- **AC-14**: Given the implementer's revision, when it finishes then the
  project's formatter and checks run and the code reviewer judges the change,
  with the same bounded retries as a first build (three attempts, findings
  fed back to the implementer); if all are exhausted the result is
  `BuildRejected("implementation", <last findings>)`.
- **AC-15**: Given an accepted revision, when it is applied then the changed
  paths are committed to the branch with the message
  `Revise after review: <title>` (title as `ProposalTitle.For("Build", ...)`
  gives for the specification), pushed, and then auto-merge is re-requested
  with `gh pr merge <url> --auto --merge` (new method `Resume` on
  `IChangeProposing`), and then the reply is posted.
- **AC-16**: Given a successful revision, when the reply is posted then it is
  a comment on the pull request reading
  `Revised the implementation in response to the review above; see the latest commit.`
  and the report is
  `<id>: <that sentence> (<address>)`.
- **AC-17**: Given a build review with no feedback, when `Revise(id)` runs then
  nothing runs and the report is
  `<id>: no feedback to revise from; nothing was run.`
- **AC-18**: Given the implementer, formatter, checks, or push fail, or the
  attempts are exhausted, when the revision stops then auto-merge is not
  re-requested, no reply is posted, the failure is returned (so the run loop
  escalates it to a maintainer on the item as for any failure), and the
  workspace is still removed.
- **AC-19**: Given re-requesting auto-merge fails after the push, when the
  revision stops then the failure is returned and no reply is posted.
- **AC-20**: Given the pipeline has no `BuildCommand` configured, when `Revise`
  is asked for a build review then it fails with `BuildingNotConfigured`.

After the revision:

- **AC-21**: Given a pushed revision, when the next pass evaluates the pull
  request then it no longer awaits revision, because the new commit is newer
  than the feedback, and it is not held again.
- **AC-22**: Given a maintainer approves a build pull request after commenting,
  when the next pass runs then it is not held or revised; if auto-merge was
  already disabled it stays disabled, and the maintainer merges it themselves.

## Out of scope

- Re-running the test author or test reviewer in response to feedback.
- Changing the "Maintainer approval" check, governed paths, or the repository's
  merge rules; governed-path builds are held and revised like any other.
- Re-enabling auto-merge after a maintainer's approval (see AC-22).
- Reacting between passes: feedback is acted on at the next run-loop pass, not
  instantly.
- Feedback on a build that asks the implementer to change tests or the
  specification, which its role may not touch; such a revision is simply
  rejected or answered by what the implementer can do.
- Any change to how builds are first proposed.

## Decisions

- **Hold step.** Auto-merge is disabled by a separate step at the start of every
  pass, not inside `Revise`: revisions run serially and each takes minutes, so
  disabling only when a revision begins would leave other commented builds free
  to merge. A read (`ListAwaitingRevision`) does not change anything.
- **Resume after push.** Auto-merge is re-requested only after the push, so a
  failed or abandoned revision leaves the build unable to merge by itself.
- **Implementer only.** Findings go to the implementer alone, because the
  implementer's role is the one that answers for code, and `WorkflowRoles`
  forbids it to change tests or the specification.
- **Order of precedence.** If an item has both kinds of pull request open, the
  specification is read and revised first, since a specification being revised
  invalidates the build.
- **Reply wording** mirrors the specification's, naming the implementation.
- **Approved builds** are neither held nor revised, matching
  `AwaitsRevision` for specifications; a build left disabled after a
  commented-then-approved sequence is merged by the maintainer.
- **Hold failures** do not stop the pass or other holds, matching
  `UpdateBehind`.
- **Documentation.** The `ProposalKind` comments and the architecture
  document's description of build merging are updated to say that maintainer
  feedback stops and revises a build; any C# shown there comes from tests via
  `mdsnippets`.
