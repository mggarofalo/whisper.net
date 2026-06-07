// Conventional Commits configuration. The same type/scope sets are mirrored in the CI PR-title check
// (.github/workflows/ci.yml). Keep the two in sync.
export default {
  extends: ["@commitlint/config-conventional"],
  rules: {
    "type-enum": [
      2,
      "always",
      ["feat", "fix", "docs", "refactor", "test", "chore"],
    ],
    "scope-enum": [
      2,
      "always",
      [
        "domain",
        "application",
        "logic",
        "infrastructure",
        "infra",
        "presentation",
        "ci",
        "docs",
        "tests",
        "hooks",
        "build",
      ],
    ],
    // Not every commit needs a scope.
    "scope-empty": [0],
    "header-max-length": [2, "always", 100],
  },
};
