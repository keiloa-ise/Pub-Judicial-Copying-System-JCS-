# Readable UI Versioning

## Purpose

This feature displays the running system version in the UI using a readable format, so it is easy to confirm which build is currently running.

Instead of showing only a long Git commit hash, the UI displays a version like:

```text
v2026.07.30.1442-d1ec7364
```

Format meaning:

- `v` means version.
- `2026.07.30` is the latest commit date.
- `1442` is the latest commit time.
- `d1ec7364` is the first 8 characters of the commit hash.

## Where It Appears

The version is displayed in the UI header:

- On the public pages before login.
- Inside the authenticated application after login.

Hovering over the version badge shows a tooltip with more details:

- Readable version.
- Branch name.
- Full commit hash.
- Commit date.

## Added Components

### 1. API Endpoint

The API exposes:

```http
GET /api/version
```

File:

```text
src/ResourceIQ.Jcs.Api/Controllers/VersionController.cs
```

This endpoint is anonymous because it only exposes runtime version metadata.

Example response:

```json
{
  "version": "v2026.07.30.1442-d1ec7364",
  "commit": "d1ec7364e5ef21490f5963cd59fe7fa2ebc6b668",
  "branch": "main",
  "deployedAt": null,
  "commitDate": "2026-07-30T14:42:12+03:00",
  "source": "git"
}
```

### 2. Reading Version Data From Git

When the API runs from a repository folder that contains `.git`, it reads version data from Git using:

```bash
git rev-parse HEAD
git branch --show-current
git show -s --format=%cd --date=format:%Y.%m.%d.%H%M HEAD
git show -s --format=%cI HEAD
```

The API then builds the readable version from the commit date, time, and short hash.

### 3. Environment Variable Support

If `.git` is not available, such as in some Docker or production deployments, version values can be provided through environment variables:

```text
JCS_VERSION
JCS_COMMIT
JCS_BRANCH
JCS_DEPLOYED_AT
```

The following fallback variable names are also supported:

```text
APP_VERSION
VERSION
GIT_SHA
COMMIT_SHA
GIT_BRANCH
BRANCH
BUILD_DATE
BUILD_TIME
```

Environment variables take priority over Git.

### 4. UI Version Badge

The UI component is:

```text
web/src/components/VersionBadge.tsx
```

It calls:

```http
/api/version
```

Then it displays the `version` value in the header.

The component is used in:

```text
web/src/components/SiteHeader.tsx
web/src/app/AppLayout.tsx
```

Styling is defined in:

```text
web/src/components/shell.css
web/src/app/app.css
```

## How the Version Updates

The version is based on the latest Git commit.

If files are edited but not committed, the version will not change.

The version changes when a new commit is created:

```bash
git commit
```

That is because the latest commit hash and commit timestamp change.

## Manual Workflow Without CI/CD

Since deployment is handled manually with Git, the normal workflow is:

```bash
git status
git add .
git commit -m "describe the change"
git checkout main
git merge feature/your-branch-name
git push origin main
```

After that, the Git server contains the latest `main` version.

To check the version locally from Git:

```bash
git rev-parse --short=8 HEAD
git show -s --format=%cd --date=format:%Y.%m.%d.%H%M HEAD
```

Compare those values with the version displayed in the UI.

## UI Verification

For local development, open:

```text
http://localhost:5173/api/version
```

Or call the API directly:

```text
http://localhost:5253/api/version
```

Then verify that the returned `version` matches the value displayed in the header.

## Important Notes

- `git push origin main` updates the Git server only.
- If there is a separate runtime server, that server must pull the latest code from `main` before it can run the new version.
- If the UI was already open, refresh the page to load the latest version value.
- In Docker, `.git` is usually excluded by `.dockerignore`, so pass `JCS_VERSION` and `JCS_COMMIT` during build or runtime when an exact production version is required.
