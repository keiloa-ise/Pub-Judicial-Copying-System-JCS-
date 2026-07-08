# Git Branch Naming Convention

This document defines the branch naming convention for this project. The goal is to keep branch names consistent, readable, and easy to trace back to the related task or ticket.

## Branch Format

```text
<type>/<ticket-id>-<short-description>
```



Example:

```text
feature/jcs-123-readonly-decision-number
```



If there is no ticket ID, use a short meaningful description:

```text
docs/git-branch-naming
```

## Branch Types

| Type | Purpose | Example |
| --- | --- | --- |
| `feature` | New feature development | `feature/jcs-123-user-profile` |
| `bugfix` | Fix a defect during development | `bugfix/jcs-245-login-validation` |
| `hotfix` | Critical production fix | `hotfix/jcs-301-production-login-fix` |
| `refactor` | Code restructuring without behavior changes | `refactor/jcs-178-auth-service` |
| `chore` | Maintenance, tooling, or configuration changes | `chore/update-dependencies` |
| `docs` | Documentation updates | `docs/api-documentation` |
| `test` | Test creation or updates | `test/auth-unit-tests` |
| `release` | Release preparation | `release/v1.8.0` |

## Naming Rules

- Use lowercase English letters only.
- Separate words with hyphens `-`.
- Do not use spaces or underscores `_`.
- Keep the description short and meaningful.
- Include the ticket ID whenever available.
- Create the branch from the correct base branch, usually `develop` or `main`.

## Recommended Branch Strategy

| Branch | Purpose |
| --- | --- |
| `main` | Production-ready code |
| `develop` | Integration branch for ongoing development |
| `feature/*` | New feature branches |
| `bugfix/*` | Bug fix branches |
| `hotfix/*` | Emergency production fixes |
| `release/*` | Release preparation branches |

## Creating a New Branch

1. Update the base branch:

```bash
git checkout develop
git pull origin develop
```

If the team works directly from `main`, use:

```bash
git checkout main
git pull origin main
```

2. Create the branch:

```bash
git checkout -b feature/jcs-123-readonly-decision-number
```

3. Commit and push the changes:

```bash
git status
git add .
git commit -m "Add readonly decision number field"
git push origin feature/jcs-123-readonly-decision-number
```

## Suggested Branch Name For The Current Changes

The current changes include:

- Making the decision number field read-only and automatically populated.
- Adding Swagger support with Bearer authentication.
- Adding this branch naming documentation.

Recommended branch name:

```text
feature/jcs-decision-number-and-swagger-auth
```

If the team provides a ticket ID, prefer:

```text
feature/jcs-123-decision-number-and-swagger-auth
```
