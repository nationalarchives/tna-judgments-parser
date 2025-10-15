# Repository Management Guide

This guide outlines best practices for branching, pull requests, versioning, CI/CD, and release workflows.

---

## Branching & Merging

### Branching Strategy

- Use **feature branches** for all development work.
- Name branches using the format:

`<ticket-id>-<short-description>`

**Examples:**
- `JUD-102-add-metadata-support`
- `JUD-215-fix-date-format`
- `JUD-233-urgent-parser-error`

### Naming Guidelines

- Include a related **ticket ID** (JIRA or GitHub issue) for traceability.
- Use **lowercase** and **hyphens**; avoid spaces or underscores.
- Keep names concise (no more than 5–6 words beyond the ticket ID).
- Avoid creating branches without clear context or linked work items.

---

## Pull Request (PR) Guidelines

### Before Opening a PR

- **Sync with `main`**: Rebase or merge to resolve conflicts early.
- **Run all tests** locally, if possible.

### PR Best Practices

- Keep PRs **small and focused** on a single topic.
- Avoid combining unrelated changes in one PR.
- Use clear PR descriptions that include:
  - **What** the PR changes
  - **Why** it is needed
  - **How** to test it
- Link related tickets or discussions.
- Ensure all commits are signed

### Review Process

- PRs must be reviewed by **at least one team member** familiar with the codebase.
- For changes affecting **shared or team-specific components** (for example, FCL, lawmaker), request a reviewer from the impacted team.
- Respond to comments and re-request reviews after updates.
- Merge only after:
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

Keep the primary `README.md` updated.

Include:

- CLI options
- Notable configuration or behaviour changes

Update documentation when:

- Modifying configuration formats
- Introducing significant functionality

Document updates in:

- PR descriptions
- The relevant section of the repository

---

## Example Release Workflow

1. Develop changes in a feature branch.  
2. Submit a PR with the relevant label for versioning for review and merge into `main` after:
- Passing CI  
- Receiving required approvals    
3. PR submitter merges to main
