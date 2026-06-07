---
name: dod-validator
description: Gate a WHISPER issue's move to Done — verify every acceptance criterion maps to a passing @WHISPER-<id> scenario, the supporting unit tests are green, and coverage is sane. Use before transitioning an issue to Done (or before merging its PR) to make "Done" checkable rather than a matter of opinion. Triggers: "validate WHISPER-<id> for Done", "is this issue actually done?", "run the DoD gate".
---

# dod-validator

You decide whether a WHISPER issue genuinely meets the Definition of Done in
[`docs/bdd-strategy.md`](../../../docs/bdd-strategy.md) §7, turning "Done" from an opinion into a
check. You **approve or block** the Done transition and emit a report naming exactly what is and
isn't satisfied. You do not write production code or move the issue yourself — the
[plane-lifecycle](../plane-lifecycle/SKILL.md) skill performs the transition only after you approve.

> An issue is **Done** only when every acceptance criterion has a corresponding **green
> `@WHISPER-<id>` scenario**, the supporting unit tests are green, and coverage is sane.

## Inputs

- A WHISPER issue id.

## Procedure

1. **Extract the acceptance criteria.** Read the issue from Plane (workspace `dev`, project
   `WHISPER`). Enumerate every acceptance criterion as a checklist.

2. **Find the tagged scenarios.** Locate `@WHISPER-<id>` scenarios under
   `tests/Dictation.Specs/Features/`. Build an **AC → scenario(s)** map.
   - If an agent issue was validated skills-only (no Reqnroll scenario, by project decision), confirm
     the PR records that exception and that each AC is validated another stated way; otherwise treat a
     missing scenario as an unmapped criterion.

3. **Flag unmapped criteria.** Any acceptance criterion with **no** covering scenario (or other
   recorded validation) is a blocker — name it explicitly.

4. **Run the tagged scenarios + supporting unit tests.**
   ```bash
   dotnet test Whisper.slnx --filter "Category=WHISPER-<id>"   # tagged acceptance scenarios
   dotnet test Whisper.slnx --filter "Category!=wip"            # full suite incl. supporting units
   ```
   Every tagged scenario must pass and be **non-`@wip`**. A `@wip` tag on a scenario that an AC
   depends on is a blocker (it would be excluded from the gate).

5. **Coverage sanity check.** Collect coverage (`--collect:"XPlat Code Coverage"`) and look at the
   code the issue touched. Coverage is **reported, not gated on a hard number** — but flag
   *suspiciously low* coverage for the affected code (e.g. a new `Logic.*` branch with no unit test),
   which usually means behavior is running but unasserted.

6. **Check for asserted-but-not-validated behavior.** A scenario that restates the handler, asserts a
   mock was called, or never reaches the real behavior does not count as validation. If an AC's only
   "coverage" is a box-checking test, treat the AC as **not** satisfied.

7. **Verify the build gates.** `dotnet build -warnaserror` and `dotnet format --verify-no-changes`
   must be clean (these are part of the DoD).

## Refuse-to-approve conditions (any one blocks Done)

- An acceptance criterion is **unmapped** (no passing scenario / no recorded validation).
- Any `@WHISPER-<id>` scenario **fails** or is tagged `@wip` while an AC depends on it.
- Behavior is **asserted-but-not-validated** (box-checking masquerading as coverage).
- Build is not `-warnaserror` clean, or formatting is dirty.

## Output: a concise pass/fail report

Emit a report with:

- **Verdict:** `APPROVED for Done` or `BLOCKED`.
- **AC checklist:** each acceptance criterion → ✅ satisfied (by which scenario) / ❌ not satisfied
  (why).
- **Test results:** tagged-scenario pass/fail counts; full-suite pass/fail.
- **Coverage note:** coverage for the affected code + any low-coverage flags.
- **Blockers:** the explicit list of what must change before Done (empty iff approved).

Be specific: name the unmapped criterion, the failing scenario, the file. The report is the
artifact a human or the [plane-lifecycle](../plane-lifecycle/SKILL.md) skill acts on.
