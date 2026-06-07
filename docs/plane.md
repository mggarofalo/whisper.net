# Plane workflow (WHISPER)

Work on `whisper.net` is tracked in the **WHISPER** project in Plane (workspace slug `dev`). This
document is the authoritative description of the issue lifecycle, branch naming, commit/PR
conventions, and the traceability tagging that ties a Plane issue to its executable specification.

The cardinal rule: **never leave an issue in an ambiguous state.** Every issue is either in Backlog,
genuinely In Progress, or Done — and "Done" means merged and validated, not "mostly finished."

## Lifecycle

```
Backlog  ──►  In Progress  ──►  (PR open)  ──►  Done
   │              │                  │            ▲
   │  branch +    │   conventional   │  squash-   │
   │  move state  │   commits        │  merge to  │
   └──────────────┴──────────────────┴──main──────┘
```

1. **Start work.** Create a branch from up-to-date `main`, then move the issue to **In Progress**.
   Do not skip straight to a later state.
2. **Implement.** Follow the double loop in [`bdd-strategy.md`](bdd-strategy.md): a failing
   `@WHISPER-<id>` scenario first, then xUnit red-green-refactor until it is green.
3. **Open a PR.** One issue per PR, conventional-commit title, linked to the issue. CI must be green
   (build `-warnaserror`, `dotnet format --verify-no-changes`, all non-`@wip` tests).
4. **Merge.** **Squash-merge** to `main`.
5. **Close.** Move the issue to **Done** only after the PR is squash-merged *and* every acceptance
   criterion is validated (see the DoD in [`bdd-strategy.md`](bdd-strategy.md) §7). If an acceptance
   criterion cannot be completed, do not mark Done — file a follow-up issue for the remainder.

## Branch naming

```
<type>/whisper-<id>-<short-slug>
```

`<type>` is the conventional-commit type that fits the work (`feat`, `fix`, `docs`, `refactor`,
`test`, `chore`); `<id>` is the WHISPER issue number; `<short-slug>` is a few kebab-case words.

Examples:

```
feat/whisper-16-push-to-talk-modes
docs/whisper-60-guidance-docs
chore/whisper-59-ci-pipeline
```

## Commits and PR titles

Both follow [Conventional Commits](https://www.conventionalcommits.org/). The allowed types and
scopes are defined once in `commitlint.config.mjs` and enforced two ways:

- **Locally** — a commitlint `commit-msg` hook (installed by `npm install`; see `AGENTS.md` →
  *Commit conventions*) rejects non-conventional messages before they land.
- **On PRs** — the CI `pr-title` job validates the PR title.

Types: `feat`, `fix`, `docs`, `refactor`, `test`, `chore`. Scopes (optional): `domain`,
`application`, `logic`, `infrastructure`/`infra`, `presentation`, `ci`, `docs`, `tests`, `hooks`,
`build`. The subject starts lowercase.

## Traceability: `@WHISPER-<id>` tags

Every Reqnroll scenario is tagged with the Plane issue it implements:

```gherkin
@WHISPER-114
Scenario Outline: Trailing silence beyond the threshold is trimmed before delivery
```

This gives bidirectional traceability: from an issue, grep `@WHISPER-114` to find its executable
spec; from a failing scenario, the report names the issue. The DoD validator uses these tags to
confirm every acceptance criterion maps to a passing scenario before an issue may move to Done.

## Plane CLI quick reference

Resolve the workspace/project first, then operate on issues. Commands target workspace `dev`,
project `WHISPER`.

```bash
# List issues
plane issue list -w dev -p WHISPER --all

# Move an issue to In Progress / Done (issue get/update take the issue UUID via --resource-id;
# the WHISPER-<n> sequence id does NOT resolve there — look the UUID up from `issue list`).
plane issue update --resource-id <uuid> --state "In Progress" -w dev -p WHISPER
plane issue update --resource-id <uuid> --state "Done"        -w dev -p WHISPER

# Link a PR/commit to an issue (this command DOES accept the sequence id)
plane link add --work-item-id WHISPER-<n> --url <pr-url> --title "<title>" -w dev -p WHISPER
```

The state names (`Backlog`, `Todo`, `In Progress`, `Done`, `Cancelled`) resolve by name; the issue
`get`/`update` commands need the issue's UUID, not its `WHISPER-<n>` sequence id.
