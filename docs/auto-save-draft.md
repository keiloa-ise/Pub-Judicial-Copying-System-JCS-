# Auto-Save Draft Recovery: Technical Explanation

JC-32 — power-outage resilience. Long forms (creating a request, preparing/correcting a copy) hold
unsent work in the browser; a power outage, tab crash, or expired session would lose it. This feature
persists the in-progress form state **both locally and on the server** and offers to restore it on
return. Drafts are transient recovery data — never part of the legal record.

Implemented by the `useAutoSaveDraft` hook (`web/src/hooks/useAutoSaveDraft.ts`), the `FormDraft`
entity/service/controller, and a lightweight scheduled cleanup service. 

---

## First — How the auto-save mechanism works

Two parallel tracks run while the user edits a form.

### 1) Local save (debounced → `localStorage`)

On every change to the form payload:

1. `payloadJson = JSON.stringify(payload)` is computed with `useMemo` (stable string identity, so the
   save effect only re-runs when the content actually changes).
2. A `useEffect` keyed on `payloadJson` starts a timer with **`debounceMs = 500 ms`** — it does not save
   on every keystroke, only after a short pause.
3. When the timer fires:
   - It writes to `localStorage` under the key `jcs:draft:{userId}:{formKey}` an object
     `{ formKey, role, copyRequestId, payload, updatedAt (ISO), source: "local" }`.
   - It raises `pendingSyncRef = true` (a server sync is now owed).
   - It updates the status: `saving → saved` (or `offline`).
4. The effect's cleanup **clears both timers** (the debounce timer and the inner 150 ms status timer),
   preventing leaks and set-state-after-unmount.

### 2) Server sync (periodic + on reconnect)

- A `setInterval` every **`serverIntervalMs` (default 10 s`, configurable via
  `VITE_AUTO_SAVE_DRAFT_SYNC_INTERVAL_MS`)** plus a `window "online"` listener call `syncNow()`.
- `syncNow()` is guarded by `active && online && !syncingRef && pendingSyncRef`, reads the local draft,
  and `PUT`s it to `PUT /api/form-drafts/{formKey}`.
- After a successful `PUT`, **only if the local `updatedAt` is unchanged** (the user didn't type during
  the round-trip) does it mark the row `source: "server"` and clear `pendingSyncRef`. Otherwise the
  newer local edit stays pending and is synced on the next tick — no lost keystrokes.

### Restore on return (hydrate)

On mount the hook reads the local draft **and** the server draft (`GET /api/form-drafts/{formKey}`),
picks the newest by `updatedAt` (`newestDraft`), and — once — prompts *“A saved draft exists. Restore
it?”*. The decision is cached in `sessionStorage` (keyed by `updatedAt`) so a remount does not re-ask.
Accepting calls `onRestore(payload)`; declining discards the local draft and `DELETE`s the server copy.

**In short:** immediate local save (500 ms debounce) + periodic server sync (every 10 s / on reconnect)
= resilience against power loss and session loss.

---

## Second — Deleting local copies (`localStorage`) — event-driven, not scheduled

Local copies are **not** removed by any scheduled timer. They are cleared on specific events, via two
helpers exported from the hook:

| Event | Function | What is removed |
|---|---|---|
| Submit / approve (work is committed) | `autoSave.clearDraft()` | the local draft + its restore-prompt decision + the server copy (`DELETE`) |
| Decline restore | inside the hydrate path | the local draft + the server copy |
| **Logout** | `clearAllLocalFormDrafts()` | **all** `jcs:draft:*` and `jcs:draft-prompt:*` keys |

`clearAllLocalFormDrafts()` iterates `localStorage`/`sessionStorage` and removes every key starting with
`jcs:draft:` / `jcs:draft-prompt:`. It is called from `AuthContext.logout` — this is the
**shared-machine confidentiality** control: `localStorage` is per-browser-profile, not per-user, and it
must survive a power outage (so `sessionStorage` is unsuitable), so the mitigation is to wipe it on
explicit logout while the per-user, authenticated server copy remains available for recovery.

---

## Third — Scheduled deletion — for stale **server** copies (not local)

The scheduled job targets old rows in the `FormDrafts` table, handled by
`FormDraftCleanupBackgroundService` — a lightweight hosted service (`PeriodicTimer`), with **no Hangfire
and no extra DB schema**:

```csharp
// FormDraftCleanupBackgroundService.ExecuteAsync (simplified)
if (!Enabled) return;
await Task.Delay(TimeSpan.FromMinutes(1));                 // let startup settle
using var timer = new PeriodicTimer(TimeSpan.FromHours(IntervalHours)); // default 24h
do
{
    await using var scope = scopeFactory.CreateAsyncScope();          // a DI scope per run
    var cleanup = scope.ServiceProvider.GetRequiredService<FormDraftCleanupService>();
    var deleted = await cleanup.DeleteOlderThanAsync(OlderThanDays);  // default 30 days
}
while (await timer.WaitForNextTickAsync(stoppingToken));
```

- **Registration:** `builder.Services.AddHostedService<FormDraftCleanupBackgroundService>();` in
  `Program.cs`.
- **The actual delete:** `FormDraftCleanupService.DeleteOlderThanAsync` runs a single set-based
  `DELETE FROM FormDrafts WHERE UpdatedUtc < @cutoff` via `ExecuteDeleteAsync` (no entity
  materialization), served by the `IX_FormDrafts_UpdatedUtc` index.
- **Configuration (optional):** `FormDraftCleanup:{ Enabled, OlderThanDays, IntervalHours }`.
- **Why a separate service:** the job runs with no authenticated user, so it uses
  `FormDraftCleanupService(IClock, IFormDraftStore)` which does not depend on `ICurrentUser` (unlike the
  request-scoped `FormDraftService`).

---

## The distinguishing summary

| Copy | Storage | Deletion mechanism |
|---|---|---|
| Local copy | `localStorage` (browser) | **Event-driven:** submit/approve, decline-restore, **logout** |
| Server copy | `FormDrafts` table (SQL Server) | **Scheduled:** a background service every 24 h deletes rows older than 30 days (plus immediate `DELETE` on submit / decline-restore) |

So: **local copies are cleared by events (never scheduled); the scheduled job only prunes stale
server-side drafts.** There is currently no time-based expiry of local copies on their own — if that is
desired (e.g. auto-expire local drafts after N days even without logout), it can be added via a
timestamp check on the local key at startup.
