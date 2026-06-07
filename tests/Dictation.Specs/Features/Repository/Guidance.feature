@WHISPER-60
Feature: Repository guidance is canonical and enforced
  As a contributor or agent working in this repository
  I want one canonical source of guidance and locally-enforced commit conventions
  So that humans and agents share the same rules and malformed commits are caught early

  Scenario: CLAUDE.md redirects to the canonical guidance
    Given the repository guidance files
    When a contributor opens CLAUDE.md
    Then it points them to AGENTS.md as the canonical source

  Scenario: A non-conventional commit message is rejected locally
    Given the commitlint commit-msg hook is installed
    When a contributor commits with the message "stuff"
    Then the commit is rejected

  Scenario: A conventional commit message is accepted locally
    Given the commitlint commit-msg hook is installed
    When a contributor commits with the message "feat(docs): add repository guidance"
    Then the commit is accepted
