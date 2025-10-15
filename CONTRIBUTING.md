# Repository Management Guide

This guide outlines best practices for branching, pull requests, versioning, CI/CD, and release workflows.

**Note:** This guide uses the MoSCoW method to indicate priority levels:
- **MUST**: Required, non-negotiable
- **SHOULD**: Strongly recommended, expected in most cases
- **COULD**: Nice to have, use when practical
- **WON'T**: Out of scope or explicitly not required

---

## Branching & Merging

### Branching Strategy

- **MUST** use **feature branches** for all development work.
- **SHOULD** name branches using the format: `<ticket-id>-<short-description>`

**Examples:**
- `JUD-102-add-metadata-support`
- `JUD-215-fix-date-format`
- `JUD-233-urgent-parser-error`

### Naming Guidelines

- **SHOULD** include a related **ticket ID** (JIRA or GitHub issue) for traceability.
- **SHOULD** use **lowercase** and **hyphens**; avoid spaces or underscores.
- **COULD** keep names concise (no more than 5–6 words beyond the ticket ID).
- **SHOULD** avoid creating branches without clear context or linked work items.

---

## Pull Request (PR) Guidelines

### Before Opening a PR

- **SHOULD** sync with `main`: Rebase or merge to resolve conflicts early.
- **SHOULD** run all tests locally, if possible.

### PR Best Practices

- **SHOULD** keep PRs **small and focused** on a single topic.
- **SHOULD** avoid combining unrelated changes in one PR.
- **SHOULD** use clear PR descriptions that include:
  - **What** the PR changes
  - **Why** it is needed
  - **How** to test it
- **SHOULD** link related tickets or discussions.
- **MUST** ensure all commits are signed

### Review Process

- **MUST** have PRs reviewed by **at least one team member** familiar with the codebase.
- **SHOULD** request a reviewer from the impacted team for changes affecting **shared or team-specific components** (for example, FCL, lawmaker).
- **SHOULD** respond to comments and re-request reviews after updates.
- **MUST** merge only after:
  - All **required reviews** are approved
  - All **CI checks** pass


## Versioning & Releases

The project follows **Semantic Versioning (SemVer)**.  
Version management is fully automated; there is no need to manually update `version.targets`.

### Automated Versioning

- When a pull request is submitted, the SemVer label applied (`major`, `minor`, or `patch`) determines how the version will be incremented if the PR is merged to `main`.
- After merging to `main`, the CI/CD pipeline automatically:
  - Calculates the next version based on all PRs merged since the previous release and their labels
  - Updates `version.targets` to the new version
  - Creates and pushes the corresponding Git tag (e.g. `v1.1.0`)
  - Publishes release notes and completes the release on GitHub

**Versioning Examples:**

| Label   | Example Increment  |
|---------|--------------------|
| `major` | `1.2.3 → 2.0.0`    |
| `minor` | `1.0.0 → 1.1.0`    |
| `patch` | `1.1.0 → 1.1.1`    |

**Current status:** Alpha — planning for the 1.0.0 milestone.

---

## Continuous Integration (CI)

All PRs into `main` must pass automated tests  

---

## File Structure & Usage

Core code is written in C#, targeting .NET 8.0.

---

## Documentation Expectations

**SHOULD** keep the primary `README.md` updated.

**SHOULD** include:

- CLI options
- Notable configuration or behaviour changes

**MUST** update documentation when:

- Modifying configuration formats
- Introducing significant functionality

**SHOULD** document updates in:

- PR descriptions
- The relevant section of the repository

---

## Commit Message Guidelines

**SHOULD** use **Conventional Commits** format for commit messages:

```
<type>(<scope>): <description>

[optional body]

[optional footer(s)]
```

**Common types:**
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation changes
- `refactor`: Code refactoring
- `test`: Adding or updating tests
- `chore`: Maintenance tasks

**Examples:**
- `feat(parser): add support for new court type`
- `fix(lawmaker): resolve date parsing issue`
- `docs: update README with CLI examples`

While conventional commits are recommended for clarity and consistency, they are not strictly enforced.

---

## Reporting Issues

### Bugs and Problems

If you encounter issues after code has been merged to `main`:

- **Compiler warnings or errors**: **MUST** open a GitHub issue immediately, tagging the relevant PR or commit
- **Test failures**: **MUST** report via GitHub issue with details on which tests failed and the environment
- **Runtime issues**: **SHOULD** include steps to reproduce, expected vs. actual behavior, and relevant logs

**What to include in bug reports:**
- **MUST** include: Clear description of the problem
- **SHOULD** include: Steps to reproduce
- **SHOULD** include: Expected behavior
- **SHOULD** include: Actual behavior
- **COULD** include: Environment details (.NET version, OS)
- **SHOULD** include: Relevant error messages or logs

---

## Project Structure

The repository contains multiple projects serving different purposes:

- **`src/`**: Core judgment parser (FCL work)
- **`src/lawmaker/`**: All lawmaker-related functionality
- **`src/leg/`**: Legislation Associated Document work

When contributing, ensure you're working in the correct project area for your changes.

---

## Communication

For questions, discussions, or support:

- **Slack**: Primary communication channel for the team
- **GitHub Issues**: For bug reports and feature requests
- **Pull Request Comments**: For code-specific discussions

---

## Example Release Workflow

1. Develop changes in a feature branch.  
2. Submit a PR with the relevant label for versioning for review and merge into `main` after:
- Passing CI  
- Receiving required approvals    
3. PR submitter merges to main
