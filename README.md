# Judicial Copying System (JCS)

Digitizes the Ministry of Justice judicial decision copying workflow
(Registry Head → Copyist → Reviewer → Administrator) with sequential copy numbering,
approval locking, and a permanent append-only audit trail.

See [PRD.md](PRD.md) (requirements) and [WORKFLOW.md](WORKFLOW.md) (state model).

## Structure

```
src/
  ResourceIQ.Jcs.Domain          entities, enums, state machine, invariants (BR-*)
  ResourceIQ.Jcs.Application      workflow services, abstractions, audit writer, allocator seam
  ResourceIQ.Jcs.Infrastructure   EF Core 9 (SQL Server), repositories, JWT, password hashing
  ResourceIQ.Jcs.Api              controllers, JWT auth, exception middleware
tests/
  ResourceIQ.Jcs.Tests           invariant-focused unit tests (xUnit)
web/                             React + Vite (TypeScript) SPA — Arabic / RTL-first
homepage-mockup.html             static design reference for the public shell + theme tokens
```

## Prerequisites

- .NET 9 SDK (pinned via `global.json`)
- Node 20+, npm 10+
- SQL Server (or LocalDB) — see the connection string in `appsettings.Development.json`

## Run

**Backend**
```powershell
dotnet build Jcs.sln
dotnet test  Jcs.sln
dotnet run --project src/ResourceIQ.Jcs.Api    # health check: GET /health
```

**Frontend**
```powershell
cd web
npm install
npm run dev        # http://localhost:5173  (Arabic/RTL by default; toggle to English in the header)
```

## ⚠ Before the first database migration — unresolved decisions

No EF migration has been generated **on purpose**. Two choices are expensive to change later
and must be confirmed first (PRD §11):

1. **Copy-number uniqueness scope** (decision #1) — global vs per-court vs per-court-per-year.
   Determines whether the `UNIQUE` index is single-column or composite. The default
   `ICopyNumberAllocator` (`PendingCopyNumberAllocator`) intentionally throws until this is
   set; a ready `GlobalCopyNumberAllocator` is provided for the global option.
2. **Arabic collation** — the case-/accent-aware collation for `nvarchar` content columns.

Other deliberately-unimplemented `[OPEN]` items (the workflow rejects them rather than guessing):
post-unlock state & editor (#3), return-cycle cap (#2), cancellation path (#5),
Registry-Head ↔ court scoping mechanism (#4). Confirm with stakeholders before building these.

## Security notes

- Authorization is enforced server-side on every action (BR-01…BR-06); the client is never trusted.
- Passwords are hashed (PBKDF2); plaintext is never stored or logged.
- Audit history is append-only — no update/delete path exists in code.
- Set a real `Jwt:SigningKey` and connection string via secrets/environment; never commit them.
