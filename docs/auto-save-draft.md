# Auto Save Draft

This document explains how the Auto Save Draft feature works in JCS, which files participate in the flow, and how the sync interval is configured.

## Goal

The feature protects users from losing unsent form work when the browser is refreshed, the network drops, or the user leaves and returns to a long form later.

Drafts are saved in two places:

- Browser `localStorage` for fast local recovery, including offline usage.
- The server database for recovery across sessions and devices for the same authenticated user.

The browser does not send a request to the server on every keystroke. It saves locally after a short debounce, then syncs the latest local draft to the server on a configurable interval.

## Main Files

Frontend:

- `web/src/hooks/useAutoSaveDraft.ts`
  Contains the reusable draft hook.
- `web/src/api/client.ts`
  Contains the API client methods for draft read, save, and delete.
- `web/src/features/requests/CreateRequestPage.tsx`
  Enables drafts for Registry Head copy request creation.
- `web/src/features/requests/PreparePage.tsx`
  Enables drafts for Copyist preparation and Reviewer correction.
- `web/src/features/requests/RequestDetailPage.tsx`
  Clears reviewer drafts when review actions complete outside the edit page.
- `web/vite.config.ts`
  Reads the root `.env` value and exposes it to the browser bundle.

Backend:

- `src/ResourceIQ.Jcs.Api/Controllers/FormDraftsController.cs`
  Exposes the authenticated draft endpoints.
- `src/ResourceIQ.Jcs.Api/Contracts/Dtos.cs`
  Defines draft request and response DTOs.
- `src/ResourceIQ.Jcs.Application/FormDrafts/FormDraftService.cs`
  Contains authorization, validation, and draft business rules.
- `src/ResourceIQ.Jcs.Application/Abstractions/IFormDraftStore.cs`
  Defines persistence operations used by the application layer.
- `src/ResourceIQ.Jcs.Infrastructure/Persistence/FormDraftStore.cs`
  Implements draft persistence with Entity Framework.
- `src/ResourceIQ.Jcs.Domain/Entities/FormDraft.cs`
  Defines the domain entity stored in the database.
- `src/ResourceIQ.Jcs.Infrastructure/Persistence/Configurations/ModelConfigurations.cs`
  Configures the database table, indexes, and field limits.
- `src/ResourceIQ.Jcs.Infrastructure/Persistence/Migrations/20260720090000_FormDrafts.cs`
  Creates the `FormDrafts` table.
- `src/ResourceIQ.Jcs.Infrastructure/Persistence/Migrations/20260720090000_FormDrafts.Designer.cs`
  Entity Framework migration metadata for the same migration.
- `src/ResourceIQ.Jcs.Infrastructure/Persistence/Migrations/JcsDbContextModelSnapshot.cs`
  Updates the EF model snapshot after the migration.

Tests:

- `tests/ResourceIQ.Jcs.Tests/FormDraftServiceTests.cs`
- `tests/ResourceIQ.Jcs.Tests/Fakes.cs`

## Configuration

The server sync interval is controlled from the root `.env` file:

```env
VITE_AUTO_SAVE_DRAFT_SYNC_INTERVAL_MS=10000
```

The value is in milliseconds.

Examples:

- `5000` means sync every 5 seconds.
- `10000` means sync every 10 seconds.
- `30000` means sync every 30 seconds.

The same variable is also listed in `.env.example` so new environments know about it.

Because the frontend is built by Vite and the shared `.env` file is in the repository root, `web/vite.config.ts` explicitly loads the root `.env` and exposes `VITE_AUTO_SAVE_DRAFT_SYNC_INTERVAL_MS` through `define`.

If the value is missing, invalid, or less than or equal to zero, the hook falls back to `10000`.

## Draft Identity

Every draft is scoped by:

- `userId`
- `role`
- `formKey`
- optional `copyRequestId`

The local browser key is:

```text
jcs:draft:{userId}:{formKey}
```

The server database enforces one draft per user and form:

```text
unique index: UserId + FormKey
```

This prevents users from seeing each other's drafts, and it also prevents different forms from overwriting each other.

Current form key patterns:

```text
registry-head:create-copy-request:{userId}
copyist:prepare-copy:{copyRequestId}:{userId}
reviewer:correct-copy:{copyRequestId}:{userId}
```

## Frontend Flow

### 1. A page enables the hook

Pages call `useAutoSaveDraft` with the current user, role, form key, payload, restore prompt, and restore handler.

Example responsibilities:

- `CreateRequestPage.tsx` passes a structured payload with fields such as court, room, copyist, category, urgency, and reference values.
- `PreparePage.tsx` passes the selected template, dynamic field values, sections, dissent sections, and rebuttal sections.

### 2. The hook hydrates on page open

When the page opens, `useAutoSaveDraft` checks:

- the browser local draft from `localStorage`
- the server draft from `GET /api/form-drafts/{formKey}` when online

If both exist, the hook compares their `updatedAt` timestamps and picks the newest one.

### 3. The user is asked whether to restore

If a draft exists and the page supplied an `onRestore` callback, the hook shows a confirmation prompt.

If the user accepts:

- the latest payload is passed to `onRestore`
- the page writes that payload back into its React state
- the latest draft is also written locally

If the user rejects:

- the local draft is removed
- the server draft is deleted when online

The hook stores the prompt decision in `sessionStorage` using the draft timestamp. This avoids repeated prompts during React Strict Mode remounts for the same unchanged draft.

### 4. Local save happens after debounce

When the payload changes, the hook waits for the debounce delay.

Default debounce:

```text
500 ms
```

After the debounce, it writes the draft to `localStorage` with:

- form key
- role
- optional copy request id
- JSON payload
- `updatedAt`
- source `local`

At this point the draft is already recoverable in the same browser, even if the network is offline.

### 5. Server sync runs on interval

The hook uses `setInterval` to call `syncNow`.

The interval comes from:

```env
VITE_AUTO_SAVE_DRAFT_SYNC_INTERVAL_MS
```

`syncNow` only sends a request when:

- the hook is active
- the browser is online
- there is a local draft
- there are pending local changes
- another sync is not already running

The request is:

```http
PUT /api/form-drafts/{formKey}
```

The payload contains:

- `payload`
- `updatedAt`
- `copyRequestId`

When the server confirms the save, the local draft is rewritten with source `server`, and pending sync is cleared.

### 6. Offline behavior

When the browser is offline:

- local saves continue
- the UI status becomes offline
- server sync is skipped

When the browser comes back online:

- the hook immediately calls `syncNow`
- the latest local draft is sent to the server

### 7. Successful submit clears the draft

After a form action succeeds, the page calls `autoSave.clearDraft()`.

This removes:

- the local `localStorage` draft
- the restore prompt decision from `sessionStorage`
- the server draft through `DELETE /api/form-drafts/{formKey}` when online

This is done after successful create, submit, approve, or similar workflow completion so completed work is not restored later as a stale draft.

## Backend Flow

### API controller

`FormDraftsController` exposes four routes:

```http
GET    /api/form-drafts/{formKey}
PUT    /api/form-drafts/{formKey}
DELETE /api/form-drafts/{formKey}
DELETE /api/form-drafts/admin/old?olderThanDays=30
```

All endpoints require authentication.

The cleanup endpoint requires the `Administrator` role.

### Application service

`FormDraftService` performs the real rules:

- requires an authenticated user
- validates that `formKey` is present and no longer than 200 characters
- validates that the draft payload is valid JSON
- checks access to the related copy request when `copyRequestId` exists
- creates a new draft when none exists
- updates the existing draft for the same user and form key when one exists
- deletes old drafts for admin cleanup

### Access rules

For drafts tied to a copy request:

- Copyists can draft only their assigned copy.
- Copyist drafts are allowed only while the copy is editable: `InPreparation` or `Unlocked`.
- Reviewers can draft only while the copy is `UnderReview`.
- Registry Heads and Administrators are allowed by role, with the normal court access rules applied where relevant.

Delete is more permissive about workflow state so a draft can still be cleaned up after a successful transition.

### Persistence

`FormDraftStore` reads and writes `FormDraft` entities through EF Core.

The database table stores:

- `Id`
- `UserId`
- `Role`
- `FormKey`
- `CopyRequestId`
- `PayloadJson`
- `CreatedUtc`
- `UpdatedUtc`
- `LastSyncedUtc`

Important indexes:

- unique index on `UserId + FormKey`
- index on `CopyRequestId`
- index on `UpdatedUtc`

The `UpdatedUtc` index supports the admin cleanup operation.

## Why There Are Two Migration Files

The migration has two C# files with the same timestamp because this is Entity Framework's normal migration structure:

- `20260720090000_FormDrafts.cs`
  Contains the executable migration steps, such as `CreateTable`, indexes, and rollback logic.
- `20260720090000_FormDrafts.Designer.cs`
  Contains EF metadata for the model at the time the migration was created.

They represent one migration, not two separate migrations.

`JcsDbContextModelSnapshot.cs` is also updated so EF knows the latest expected model shape for future migrations.

## Status Values

The hook exposes a status value so pages can show lightweight feedback:

- `idle`
- `saving`
- `saved`
- `offline`
- `syncing`
- `synced`
- `error`

Pages translate these statuses into UI text using the existing localization helpers.

## Failure Handling

If local parsing fails, the hook ignores the corrupted local draft instead of crashing the page.

If the server cannot be reached:

- the local draft remains
- pending sync remains true
- the user can continue editing
- the next interval or online event can retry sync

If server authorization fails, the API returns an error and the UI status becomes `error`.

## Test Coverage

The service tests cover:

- creating, reading, updating, and deleting a draft
- preventing a copyist from drafting someone else's request
- allowing cleanup after workflow state changes
- deleting old drafts through the admin cleanup path

Frontend build verification also checks that the hook, API client, and page integrations compile together.
