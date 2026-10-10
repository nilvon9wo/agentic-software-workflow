# Keep a maintainer's approval when only the base branch has been merged in

## Summary

The "Maintainer approval" check (`scripts/maintainer_approval.py`) accepts a
maintainer's approval only when it was given on the pull request's exact head
commit. Because the ruleset requires pull requests to be up to date, every
merge of the base branch into an approved pull request creates a new head
commit and silently voids the approval. This item makes an approval given on an
earlier commit still count for the current head commit if and only if the pull
request's own changes (the three-dot diff of base and commit, as in
`git diff base...commit`) are identical at both commits. Any difference in the
pull request's own changes needs a fresh approval. The maintainer accepted this
proposal on the work item.

The check still runs from the base branch and never checks out the pull
request. It obtains the two diffs through the GitHub CLI (`gh`), using the
compare endpoint
`repos/<owner/repository>/compare/<base>...<commit>`, where `<base>` is the
pull request's base branch name (`.base.ref`).

Two diffs are identical when their ordered lists of changed files are equal,
comparing for each file its path, status, previous path (for renames) and
resulting blob SHA. The blob SHA stands in for byte-for-byte content, so binary
files and mode-neutral content are covered without comparing patch text.

## Acceptance criteria

- **AC-1**: Given a pull request that changes a governed path, whose head
  commit `H2` differs from the commit `H1` a maintainer approved, and whose
  diff against its base is identical at `H1` and `H2` (only base-branch merges
  were added), when `main` runs, then it returns exit code 0 and prints a
  reason naming the approving maintainer, the approved commit `H1` and the
  head commit `H2`.
- **AC-2**: Given the same setup as AC-1 but the diff at `H2` differs from the
  diff at `H1` in any file's content (an extra edit, an added file, or a
  removed change), when `main` runs, then it returns exit code 1 and prints the
  existing missing-approval message naming the governed paths and head commit
  `H2`.
- **AC-3**: Given a base-branch merge with a conflict resolution that alters
  what the pull request proposes (so the diff at `H2` differs from that at
  `H1`), when `main` runs, then it returns exit code 1, as in AC-2.
- **AC-4**: Given the diff at `H2` differs from that at `H1` only in a file's
  status, path or previous path (for example a rename that became an add), when
  `main` runs, then it returns exit code 1.
- **AC-5**: Given the diff at `H2` is identical to the diff at `H1` except that
  the files are listed in a different order, when `main` runs, then it still
  returns exit code 1 only if the contents differ; order alone is compared as
  GitHub returns it, so both lists must be equal in order to match. (Tests use
  the order the compare endpoint returns.)
- **AC-6**: Given a maintainer approved the current head commit exactly, when
  `main` runs, then it returns exit code 0 with the existing
  `Approved by <names> at <head>.` reason and makes no compare calls.
- **AC-7**: Given the pull request changes no governed path, or is authored by
  a maintainer, when `main` runs, then it returns exit code 0 with the existing
  reason and makes no compare calls.
- **AC-8**: Given a maintainer approved `H1`, but the same maintainer's latest
  review is not `APPROVED` (for example `CHANGES_REQUESTED` or `DISMISSED`),
  when `main` runs, then that earlier approval is not carried forward and the
  result is exit code 1.
- **AC-9**: Given the approving review's author is not listed in `maintainers`
  in `aswf.json`, when `main` runs, then the review is not carried forward even
  if the diffs are identical, and the result is exit code 1.
- **AC-10**: Given several maintainers approved different earlier commits, when
  `main` runs, then each approved commit is compared at most once per run, and
  the check passes if any of those approvals has an identical diff; the reason
  names every maintainer whose approval applies.
- **AC-11**: Given the compare call for the head commit or for an approved
  commit fails (for example `gh` exits non-zero because the commit no longer
  exists after a force-push), when `main` runs, then the failure propagates
  (`subprocess.CalledProcessError`) and the check does not pass.
- **AC-12**: Given a pull request with a governed change and no approving
  review at all, when `main` runs, then no compare calls are made and the
  result is exit code 1.
- **AC-13**: `docs/how-it-works/quality-gates.md` describes the carry-forward
  rule: what makes an approval carry over, what voids it, and the remaining
  risk (the check trusts GitHub's compare output). No C# or hand-typed code
  appears in that document.

## Out of scope

- Removing the "up to date before merging" requirement from the ruleset (the
  alternative named in the item).
- Changing `GOVERNED_PATHS`, the author-is-maintainer rule, or who counts as a
  maintainer.
- Carrying forward reviews other than `APPROVED`, or approvals by non-maintainers.
- Checking out or executing the pull request's code.
- Any change to how the loop brings pull requests up to date (#58).

## Decisions

- The diff is fetched with the GitHub compare endpoint against the pull
  request's current base branch name for both commits, because the check has no
  checkout and `git diff base...head` is what the compare endpoint computes.
- Identity of diffs is judged on path, status, previous path and blob SHA per
  file, rather than patch text, because patches are absent for binary and large
  files while the blob SHA always represents exact content.
- A failing compare call fails the check loudly instead of being treated as
  "no match", consistent with how `run_gh` already treats failures, and it
  fails closed. The cost is that a force-pushed-away approved commit makes the
  check error until a fresh approval is given.
- Only each maintainer's latest review is considered, preserving the existing
  `latest_decisions` semantics, so a later rejection or dismissal revokes an
  earlier approval.
- Comparing is done only when the cheaper existing reasons do not apply, to
  avoid needless API calls.
