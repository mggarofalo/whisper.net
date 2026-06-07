---
name: plane-lifecycle
description: Perform a WHISPER issue's Plane state transitions consistently — branch from the issue, move it to In Progress, open a linked conventional-commit PR, and move it to Done only after the PR is squash-merged. Use to start, advance, or close out an issue without leaving it in an ambiguous state. Idempotent and safe to re-run. Triggers: "start WHISPER-<id>", "open the PR for <id>", "close out <id>", "transition <id> to Done".
---

# plane-lifecycle

You perform the WHISPER issue bookkeeping so an issue is never left in an ambiguous state. You drive
the transitions in [`docs/plane.md`](../../../docs/plane.md):
**Backlog → In Progress → (PR open) → Done**, never skipping a step. You are the orchestration layer
around [spec-author](../spec-author/SKILL.md) → [implementer](../implementer/SKILL.md) →
[dod-validator](../dod-validator/SKILL.md); you do not write specs or production code yourself.

Plane context: workspace `dev`, project `WHISPER`.

## Safety first: check current state, refuse surprises

Before any transition, **read the issue's current state** and confirm it is what this step expects.
If it isn't, **stop and surface it** — do not force the transition.

- Asked to *start* an issue already `In Progress` or `Done` → report it; don't re-open or duplicate
  the branch. (Re-running is safe: if the branch already exists and the state is already
  `In Progress`, that's success, not an error.)
- Asked to move to *Done* an issue with no open/merged PR, or whose PR isn't merged → **refuse** and
  say why.
- Issue in `Cancelled` → refuse to proceed; surface for a human.

This idempotence is the point: running the skill twice must never create a second branch, skip a
step, or jump straight to Done.

## Transitions

### 1. Start work (→ In Progress)

```bash
# Branch from up-to-date main, named per docs/plane.md: <type>/whisper-<id>-<slug>
git checkout main && git pull --ff-only
git checkout -b <type>/whisper-<id>-<short-slug>

# Move the issue to In Progress (get/update take the issue UUID via --resource-id; the WHISPER-<n>
# sequence id does NOT resolve there — look the UUID up from `issue list` first).
plane issue update --resource-id <uuid> --state "In Progress" -w dev -p WHISPER
```

`<type>` is the conventional-commit type fitting the work (`feat`, `fix`, `docs`, `refactor`,
`test`, `chore`).

### 2. Open a PR (work ready for review)

```bash
git push -u origin <branch>
gh pr create --repo mggarofalo/whisper.net --base main \
  --title "<type>(<scope>): <subject>" --body-file <pr-body>      # Conventional Commits title

# Link the PR to the issue (this command DOES accept the WHISPER-<n> sequence id).
plane link add --work-item-id WHISPER-<id> --url <pr-url> --title "<title>" -w dev -p WHISPER
```

The PR title must pass the CI `pr-title` check (Conventional Commits, lowercase subject). One issue
per PR.

### 3. Close out (→ Done)

**Preconditions, all required:**
- The [dod-validator](../dod-validator/SKILL.md) has **APPROVED** the issue.
- CI is green on the PR.
- The PR is **squash-merged to `main`** (verify, don't assume).

```bash
gh pr merge <n> --repo mggarofalo/whisper.net --squash --delete-branch
gh pr view <n> --repo mggarofalo/whisper.net --json state,mergedAt   # confirm MERGED
plane issue update --resource-id <uuid> --state "Done" -w dev -p WHISPER
```

**Never** move an issue to `Done` before its PR is merged. If acceptance criteria remain that can't
be completed, do **not** mark Done — file a follow-up WHISPER issue for the remainder and report it
(documenting deferred work in a description does not count).

## Never-skip rule

The order is fixed: a branch + `In Progress` precede a PR; a merged PR precedes `Done`. Do not jump
from Backlog straight to Done, and do not mark Done on an unmerged PR. If you're tempted to skip a
step, that's the signal to stop and surface the situation instead.

## Plane CLI notes (gotchas)

- `issue get` / `issue update` need the issue **UUID** via `--resource-id`; the `WHISPER-<n>`
  sequence id does **not** resolve there. Get the UUID from `plane issue list -w dev -p WHISPER --all`.
- `plane link add --work-item-id WHISPER-<n>` **does** accept the sequence id.
- State names (`Backlog`, `Todo`, `In Progress`, `Done`, `Cancelled`) resolve by name.

## Output

Report the action taken and the resulting state: branch name + new state on start; PR url + link on
PR; merge confirmation + `Done` on close. On refusal, state the current issue state and exactly which
precondition failed.
