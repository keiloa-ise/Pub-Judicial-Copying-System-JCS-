# Judicial Copying System (JCS) — Product Requirements Document

**Project:** Judicial Copying System (JCS)
**Prepared for:** Ministry of Justice
**Status:** Revised draft

> Items marked **[OPEN]** are decisions the original PRD left undefined. They are
> called out inline rather than silently assumed, and should be resolved with
> stakeholders before implementation begins.

---

## 1. Executive summary

The Judicial Copying System (JCS) is a workflow management platform that digitizes and
automates the judicial decision copying process currently performed manually between
Court Registry Heads, Copyists, Reviewers, and System Administrators.

The system ensures accountability, workflow enforcement, document accuracy, auditability,
and operational efficiency while maintaining strict role separation and approval controls.
It provides a centralized environment for creating, preparing, reviewing, approving,
tracking, and reporting judicial copy requests.

## 2. Business problem

Judicial copy preparation is currently manual and suffers from:

- Paper-based tracking and manual assignment
- Lack of visibility and delayed approvals
- Human errors and missing audit history
- Difficulty monitoring workloads and measuring productivity

These issues increase processing time and reduce operational transparency.

## 3. Business objectives

- Digitize the copy preparation process
- Reduce manual intervention
- Enforce judicial workflow rules
- Improve tracking and accountability
- Reduce approval delays
- Provide complete, permanent audit history
- Standardize judicial document preparation

## 4. Product goals

**Primary:** centralized copy management, automated workflow routing, controlled approval
process, court-specific form management, real-time tracking.

**Secondary:** reduced processing time, improved document quality, increased transparency,
improved management oversight.

## 5. User roles

### Administrator
Manage system configuration, users, courts, judges, permissions, paragraph templates,
and dynamic forms. Unlock approved copies.

### Head of Registry (رئيس الديوان)
Create copy requests, select judicial decisions, enter case information, submit requests. May also
**delete the most-recently-created copy on the system** (within their courts) to undo a mis-entry
(FR-16).

### Copyist (الناسخ)
Prepare copy content, complete form fields, insert approved paragraphs, submit for review.

### Reviewer (المدقق)
Verify copied content and ensure accuracy. For a copy under review the Reviewer has three
options: **approve** the copy, **correct it directly** (edit the content in place, then approve),
or **return it for correction** by the assigned Copyist.

## 6. Workflow overview

| Stage | Actor | Action |
|-------|-------|--------|
| 1 | Registry Head | Creates a copy request |
| 2 | System | Generates sequential copy number |
| 3 | System | Request appears in assigned Copyist's queue |
| 4 | Copyist | Completes required content |
| 5 | Copyist | Submits for review |
| 6 | Reviewer | Reviews document |
| 7A | Reviewer | Approves → copy is locked |
| 7B | Reviewer | Returns for correction (→ Copyist) |
| 7C | Reviewer | Corrects the content directly (stays Under review), then approves |
| 8 | Administrator | May unlock an approved copy when necessary |

**Return path (7):** The stage a returned or unlocked request re-enters must be defined.
Assumed target: back to the assigned Copyist (state `In preparation`). Confirm whether
there is a limit on the number of review/return cycles.

**Post-unlock state (Stage 8) — RESOLVED:** an unlocked copy is re-edited by the **assigned
Copyist** and re-submitted to the **Reviewer** for approval (Unlocked → Under review → Approved).
The copy keeps its existing copy number.

**[OPEN] Cancellation:** There is currently no path to void or cancel a request entirely
(distinct from "return for correction"). Confirm whether cancellation is required and
which role may perform it.

See `WORKFLOW.md` for the full state diagram.

## 7. Functional requirements

### FR-01 — Authentication
Users can log in securely and log out.
**Acceptance:** authentication required; unauthorized access blocked.

### FR-02 — User management
Administrator can create, edit, and disable users, and assign roles.
**Acceptance:** role assignment mandatory; user status tracked.

### FR-03 — Court & room management
Administrator can create, edit, activate, and deactivate courts, and manage the rooms (غرف)
within each court.

**رقم النسخة (عادي) numbering policy — per room:** when creating (or editing, before any number has
been issued) a room, the Administrator chooses the copy-numbering scope:
- **مستوى الغرفة (Room level, default):** each room has its own sequential رقم النسخة; the number embeds
  the room code — `{courtCode}/{roomCode}/{year}/{seq}` — so rooms never collide.
- **مستوى المحكمة (Court level):** all court-level rooms of the court share one sequence —
  `{courtCode}/{year}/{seq}`.
Both reset yearly. The policy **cannot be changed once the room has issued any رقم النسخة** (it would
mix scopes). Existing rooms were migrated to **room level**. Start points (FR-17) can be seeded at go-live.

For each room the Administrator also sets the **رقم المتفرق numbering policy**:
- **مستوى المحكمة (Court level):** متفرق copies of the room share one sequence with all of the
  court's court-level rooms.
- **مستوى الغرفة (Room level):** the room has its own sequence.
- **مستوى خاص (Special level A–Z):** the room joins a shared sequence identified by a letter A–Z.
  **Special levels are defined PER COURT** — level "A" of court X is a different sequence from
  level "A" of court Y — so rooms of the *same* court can be grouped onto one shared sequence.

All scopes reset yearly. A dedicated **read-only screen** lets the Administrator browse, per court,
the special levels and the rooms grouped on each (plus the court-/room-level rooms). Default policy
for the seeded rooms: جزائية → room level, all other courts → court level.
**Acceptance:** court code unique; **court name unique (global)**; **room name unique within its court**
(BR-14); a Special-policy room requires a level A–Z.

### FR-04 — Judge management
Administrator can create, edit, activate, and deactivate judges, and assign a judge to a court.
**Acceptance:** judge assignable to one or more rooms; **judge name unique (global)** (BR-14).

### FR-05 — Scope assignment (rooms / courts)
Administrator assigns scope by role (BR-06): **rooms** to Copyists and Reviewers (one or more
rooms, which may span several courts), and **courts** to Registry Heads. Administrators are
unrestricted. Assignment is on the Users screen — a room picker (grouped by court) for
copyist/reviewer, a court picker for the head.
**Acceptance:** a Copyist/Reviewer sees and acts on copies only in their assigned rooms; a
Registry Head only within their assigned courts; enforced server-side on every action.
**[RESOLVED]** Registry Heads are court-scoped (via `UserCourt`); Copyists/Reviewers are
room-scoped (via `UserRoom`), with their court scope derived from their rooms' courts.

### FR-06 — Create copy request
Registry Head can select court and room, choose the assigned copyist, enter the case base
number, and optionally the **case-filing date (قيد الدعوى)**, plus the classifications below.
**تاريخ الحجز (reservation date) is NOT entered** — it is **assigned by the server** at creation
(the current date) and is not editable; it also drives the numbering year (FR-18).
**رقم الأساس** must be **unique per court for عادي copies** (no two عادي copies in a court may share
the same رقم الأساس; متفرق copies inherit the original's and are excluded — BR-12).
- **التصنيف (Category):** عادي (default) / متفرق. A **متفرق** decision is **based on an existing
  Approved عادي copy** (النسخة الأصلية, BR-11): the Registry Head selects that original; the متفرق
  **does NOT get a رقم النسخة** — only a **رقم المتفرق** allocated by the **room's numbering policy**
  (FR-03; reset yearly) — and it is **linked** to the original, **inheriting** its court, room and
  رقم الأساس. **رقم المرجع is optional.** One original copy may have many متفرق decisions; رقم المتفرق
  is shown on the printed copy, which also references the original copy number.
- **الحالة (Status):** عادي (default) / موقوف / مستعجل — drives work-queue **execution priority**:
  **موقوف > مستعجل > عادي**. When **مستعجل** is chosen, an **expedite-request number
  (رقم طلب الاستعجال)** is mandatory.
  - **Escalation (BR-13):** a **non-approved** copy may be escalated to **مستعجل** at any time by the
    Registry Head (requires the expedite-request number), which **raises its priority** immediately.
  - **Suspension escalation:** a **non-approved** copy may also be escalated to **موقوف** at any time
    by the Registry Head, including copies that are currently **عادي** or **مستعجل**. This is the
    highest priority tier, does **not** require an expedite-request number, and once a copy is
    **موقوف** it cannot be downgraded back to **مستعجل** or **عادي** through the escalation flow. An
    **optional note (رقم طلب التصعيد / ملاحظة)** may be captured with the escalation and is stored on
    the copy (and in the audit entry).

The former "مرجع الحكم" field and the "الإجراء" (procedure) field were removed.
**Acceptance:** a sequential copy number is generated automatically and atomically for عادي copies;
the reservation date is server-assigned (not a client input); a **duplicate رقم الأساس for a عادي copy
in the same court is rejected**; مستعجل without an expedite-request number is rejected; non-approved
عادي/مستعجل copies may be escalated to موقوف; متفرق without a (selected) Approved original is rejected
and gets a رقم المتفرق but no رقم النسخة.

### FR-07 — Copy preparation
The Copyist must **accept (قبول)** an assigned copy **before** editing it — editing or submitting a
copy that has not been accepted is rejected. **Acceptance is enforced in a strict order (BR-10):** by
priority tier **موقوف ← مستعجل ← عادي**, and within a tier **oldest first** (earliest created). A Copyist
cannot accept a copy while one of theirs ranks before it (higher tier, or same tier but older). The
**acceptance time is recorded** on the copy (for reporting — see FR-13), and accepted copies are
**highlighted** (distinct colour) in the work queue. After acceptance the Copyist edits assigned
requests, completes form fields, adds legal paragraphs, and saves drafts.
- **Copyist queue display (طلباتي):** the **single** copy the Copyist may accept next (top-ranked
  unaccepted) is shown with a **faint-red** row; any **other** unaccepted copy is **blurred and cannot
  be opened** until the higher-priority ones are accepted (enforcing the strict order visually). An
  unaccepted copy's status reads **«بانتظار القبول»** (not «قيد التحضير») until it is accepted.
- **Approved visibility (config):** whether **Approved** decisions appear in the Copyist's and
  Reviewer's «طلباتي» queue is controlled by the **`SHOW_APPROVED_IN_QUEUE`** flag (default off);
  when off they are hidden to keep the working queue focused.
**Acceptance:** edit/submit before acceptance is rejected; acceptance must follow tier-then-oldest order
(no skipping); the acceptance timestamp is stored; drafts may be saved multiple times.
- **Hijri date:** when the Copyist enters the Gregorian issue date, the system auto-fills the
  corresponding Hijri date (Umm al-Qura). The Hijri value remains editable for manual override.
- **Arabic spell-check:** free-text content fields (section titles and bodies, and text form
  fields) have Arabic spell-checking enabled to assist the Copyist. Legal text is never silently
  altered — suggestions are advisory only.
- **رقم القرار (auto):** the decision number is **not** typed by the Copyist — it is auto-filled from the
  copy's own sequential number (رقم النسخة) and shown read-only.
- **Dissent (مخالفة القضاة):** the Copyist may mark one or more panel judges — including the room
  president — as **dissenting (مخالف)** and author a dissent appendix stating the reason; see **FR-19**.
- **Reply to dissent (الرد على المخالفة):** when a dissent exists, the Copyist may mark one or more of
  the **non-dissenting** judges as authoring a **reply (رد)** and write a reply appendix; see **FR-20**.

### FR-08 — Dynamic forms
Administrator can create form templates, define fields, and define validation rules.
**Acceptance:** forms render dynamically from their template.

### FR-09 — Paragraph templates
Administrator can create, edit, and archive paragraph templates. Copyists can insert
approved (non-archived) templates only.
**Acceptance:** archived templates are unavailable for insertion.

### FR-10 — Review workflow
Reviewer can approve a request, correct its content directly, or return it for correction.
**Acceptance:** approval records reviewer identity and timestamp. Direct correction edits the
content while the copy stays *Under review* (no bounce to the Copyist) and is recorded as an
audited Edit whose actor is the Reviewer; the Reviewer then approves. Return requires a
mandatory corrections note and sends the copy back to the assigned Copyist.
- **Approval priority order (BR-10):** the Reviewer must **approve** decisions in the **same order the
  Copyist accepts** — priority tier (موقوف > مستعجل > عادي) then **oldest-first** within a tier. A copy
  cannot be approved while a higher-ranked copy is still *Under review* in the reviewer's courts.
  (Only approval is ordered; direct correction and return are not.)

### FR-11 — Approval locking
The system locks approved copies.
**Acceptance:** no modifications allowed after approval through any normal write path.

### FR-12 — Unlock process
Administrator can unlock approved copies.
**Acceptance:** unlock reason mandatory; audit entry mandatory.

### FR-13 — Reporting
The system provides reports of copies **per court, room, copyist (ناسخ/كاتب), reviewer (مدقق), registry
head (رئيس الديوان), and judge (قاض)**, with date-range/status filters, a turnaround-time report
(creation → approval), and an **average time-to-acceptance** (creation → Copyist acceptance, FR-07)
headline metric.
- **Per-judge** productivity is **approximate**: judges are stored as free-text names in each copy's
  panel (president/members) rather than as structural links, so the report aggregates Approved copies
  by the judge names found in their content.
- **Judge work log (سجل أعمال القضاة):** a **detailed** report — between two **تاريخ الحجز** dates and
  across **all states** — listing, **per judge**, every decision they sat on with the **role** (رئيس/عضو),
  court/room, رقم القرار, state, and — when they were **delegated (ندب)** — the **delegation date and
  decision number** (all read from the panel stored in `FieldValuesJson`). Exportable (CSV/Excel) like
  the other reports and scoped server-side by role + courts (BR-06).
- **Stage timeline (FR-13 UX):** the copy detail page shows a **per-stage timeline** — each workflow
  milestone (إنشاء/قبول/تحضير/مراجعة/اعتماد…) with the **time spent in that stage**, derived from the
  append-only audit trail.

### FR-14 — Notifications *(proposed)*
The system notifies the relevant actor when work enters their queue (Copyist on assignment,
Reviewer on submission, Copyist on return).
**[OPEN]** Confirm whether notifications are in scope and the channel (in-app, email).

### FR-15 — Printed copy (إعلام الحكم)
The copy can be printed as the official "إعلام الحكم" document.
**Acceptance:**
- The document is rendered to a **PDF on the server** (from the authoritative record) and streamed
  to the browser's viewer for printing/download. The client never builds the printable content as
  editable DOM, so it cannot be altered (e.g. via dev-tools) before printing — this is the integrity
  control against pre-print tampering. (Note: this prevents pre-print DOM tampering; it does not, by
  itself, prevent editing a downloaded PDF offline — out of scope, no digital signature required.)
- The case base number (رقم الأساس) is **not** printed on the page (neither as visible text nor
  inside the QR payload). For متفرق copies, **رقم المتفرق** is shown on the page.
- If the copy is **not approved** (any state other than *Approved*), every page is stamped with a
  clear, repeated **"مسودة قرار"** watermark, so a draft can never be mistaken for a final copy.
  Once approved, the watermark is absent.
- If any judge **dissents (FR-19)**, the decision page shows — **before the judges' signatures** — a
  note that a dissenting opinion exists, naming the dissenting judges; and a **dissent appendix** is
  printed on a **new page** after the decision (reason sections + signatures of the dissenting judges
  only).
- If one or more judges **reply to the dissent (FR-20)**, a **reply appendix** («الرد على الرأي
  المخالف») is printed on a **new page after the dissent appendix** — reason sections + signatures of
  the replying judges only.

#### Print policy
- **Ordered printing (R1):** a decision's FIRST print follows priority + sequence — **موقوف > مستعجل >
  عادي**, then oldest-first — within its court and its queue (approved vs non-approved, ordered
  independently). **On approval, a decision is NOT auto-printed** — it enters the **reviewer print queue**
  (see below) from which the reviewer prints it. **Re-print (R3):** after its first print an approved copy
  may be viewed and **re-printed at any time** (no unlock/re-approval needed). Preview (`GET /pdf`) is
  read-only and never marks a copy printed; printing (`POST /print`) is the controlled action (records
  the print, audited as `Print`).
- **Batch print — Administrator (FR-15/BR):** between two **تاريخ الحجز** dates for a specific court+room,
  of one kind (مثبتة/مسودة); **each decision is rendered to its own PDF** and delivered as a **ZIP**.
  Read-only export — not subject to the order/once rules and never marks copies printed.
- **Reviewer print queue (المدقق):** a queue of decisions awaiting print; selecting a decision's
  checkbox **cumulatively selects all decisions ranked before it** (e.g. checking #25 selects 1–25),
  reflecting the strict order. Selected decisions are **printed directly** and **removed from the queue**.
- **Copyist print queue (المحرر):** when a copyist **accepts** a decision (before it is finalized) it
  enters a **copyist-only print-waiting queue** shown in a **separate tab**. The copyist selects an
  **arbitrary** set from the queue; they are **printed directly** and **removed from the queue**.
- **Feature flags** (`.env`, read by the API):
  - **`ALLOW_COPYIST_REPRINT`** (default **true**): the individual reprint option is available to the
    **Copyist + Registry Head**; when **false**, only the **Registry Head**.
  - **`ALLOW_HEAD_BATCH_PRINT`** (default **true**): the batch-print tab is available to the
    **Administrator + Registry Head** (per permissions); when **false**, only the **Administrator**.

### FR-16 — Delete a last decision (Registry Head)
Deletion is performed only through a dedicated **deletion-operations window** (no per-copy delete
button), within the Registry Head's courts, in **two sections** (current year). The copy + content are
removed (hard delete, any state incl. *Approved*); **audit history is preserved** (a `delete` entry is
appended, nothing is ever removed):
- **عادي** — the **latest copy per court** (highest رقم النسخة of the year). Rolls back the **رقم النسخة**
  counter. **Disabled** when the copy has **linked متفرق** decisions (deleting it would orphan them, BR-09)
  — those متفرق must be deleted first.
- **متفرق** — the **last متفرق per numbering scope** (highest رقم المتفرق in that scope/year). Rolls back
  only the **رقم المتفرق** counter (a متفرق has no رقم النسخة).

Each section's row is deletable only when it is the last in its sequence, so no gap appears; a
confirmation popup shows the decision's details before deletion.
**Acceptance:** only a Registry Head; only within their courts (BR-06); عادي deletable only when it is the
court+year latest copy AND has no linked متفرق; متفرق deletable only when it is the last in its scope; a
`delete` audit entry is written; no gap in either sequence.

### FR-17 — Numbering start points (go-live setup)
When the system goes live, decisions already exist in the courts and their sequential numbers were
issued by the previous (manual) process. The Administrator must therefore seed the **starting point**
of each auto-generated sequence so the system continues from where the manual numbering stopped —
**without restarting from 1 and without colliding** with already-issued numbers.

Two sequences are seeded, both via a self-service Administrator screen (**«ضبط بدايات الترقيم»**):
- **رقم النسخة** — per **court + year** (`CourtCopyCounter`). The format is `{courtCode}/{year}/{seq}`.
- **رقم المتفرق** — per **numbering scope + year** (`MiscNumberCounter`), where the scope follows the
  room's policy: court level (`C:{court}`), room level (`R:{room}`), or special level (`S:{court}:{level}`,
  per court — see FR-03).

Semantics & rules:
- The value entered is the **«آخر رقم صدر» (last issued number)**; the system issues the **next**
  number as `lastNumber + 1`.
- Set **per year** (the current year, and earlier years if decisions carry earlier reservation years),
  because both sequences **reset yearly**.
- **Collision guard:** a counter may never be set **below the highest number already used in the
  system** for that court/scope+year (parsed from existing copies). At go-live (no copies yet) any
  value ≥ 0 is accepted; once auto-issued copies exist, lowering below them is rejected.
- These counter changes are **restricted to the Administrator** (no separate audit entry — counter
  rows are not copy-scoped).
- **Order at go-live:** finalise each room's numbering **policy** (FR-03) *before* seeding رقم المتفرق
  start points, because changing a room's policy changes its scope key (and thus which counter applies).

**Acceptance:** only the Administrator can set start points; the next issued number is `lastNumber+1`;
setting below the highest used number for that court/scope+year is rejected; values are per year.

### FR-18 — Year rollover (annual numbering reset)
Both auto-generated sequences (**رقم النسخة** per court, **رقم المتفرق** per scope) reset **per year**.
The year is taken from the copy's **reservation date** (`ReservationDate.Year`), not the system clock.
- **Automatic reset to 1:** the first copy/متفرق of a new year for a court/scope creates a new
  counter row starting at `1` — **no manual action is required** at the turn of the year. The printed
  number embeds the year (`{courtCode}/{year}/{seq}`), so numbers never collide across years and prior
  years' counters are preserved.
- **Reservation-year driven:** a decision reserved late in one year but entered early in the next still
  uses the **reservation year's** sequence; the two years' counters coexist.
- **Start points (FR-17)** are per year: a new year begins at 1 automatically unless the Administrator
  seeds a different start for that year.
- Room numbering **policies** and special **levels** are permanent room attributes — they are unaffected
  by the rollover; only the yearly counters reset.
- **Deletion window (FR-16)** operates on the **current calendar year** only: after the new year starts,
  the previous year's decisions are no longer listed there (a known scoping limit of decision 4).
**Acceptance:** the first copy of a new reservation year is numbered `…/{year}/0001`; prior-year
counters are untouched; no manual reset is needed.

### FR-19 — Judges' dissent (مخالفة القضاة)
A decision may carry a **dissenting opinion (رأي مخالف)**: one or more judges of the panel — **including
the room president** — disagree with the issued decision, with the reason stated explicitly and signed by
the dissenting judges.
**Acceptance:**
- In the Copyist's preparation screen a **«مخالف» checkbox** appears next to each judge (president and
  every member); ticking it marks that judge as dissenting.
- When at least one judge dissents, a **dissent-appendix editor** appears, authored with the **same
  paragraph/template style** as the main body (the رأي مخالف reason).
- **Finalize is blocked** (submit for review / approve) if a dissent is marked with no reason text.
- The printed «إعلام الحكم» then shows **at the bottom of the decision page — before the judges'
  signatures — a note that a dissent exists, naming the dissenting judges**, and a **dissent appendix on
  a new page** after the decision: the reason sections followed by the **signatures of the dissenting
  judges only**.
- Which judges dissent is stored inside the copy's panel field values (`members[].dissenting` +
  `presidentDissenting`); the reason text is a separate content column (`DissentSectionsJson`). Both are
  **backward-compatible** — existing copies carry no dissent.
- **Delegated judges (ندباً):** any panel judge — a member **or the president** — may be a **delegated
  judge from another room or court**. A «منتدب» toggle lets the Copyist pick that judge from **all active
  judges** (not just the copy room's), and the delegated judge's capacity (صفة) is **auto-set to «ندباً»
  and locked** (non-editable). Stored as `members[].delegated` / `presidentDelegated` in the panel field
  values; the printed capacity is «ندباً». Backward-compatible — existing copies carry no delegation.

### FR-20 — Reply to dissent (الرد على المخالفة)
When a decision carries a dissenting opinion (FR-19), the panel may respond with a **reply (الرد على
الرأي المخالف)**: one or more of the **non-dissenting** judges — including the room president — author a
reply stating the majority's position, signed by the replying judges.
**Acceptance:**
- The reply is available **only when at least one judge dissents**. A **«رد» checkbox** appears next to
  each judge (president and every member); it is **enabled only for a non-dissenting judge** while a
  dissent exists. A judge can never be **both dissenting and replying** (the two checkboxes are mutually
  exclusive — ticking one clears the other).
- When at least one judge replies, a **reply-appendix editor** appears, authored with the **same
  paragraph/template style** as the main body.
- **Finalize is blocked** (submit for review / approve) if a reply is marked with no reply text.
- The printed «إعلام الحكم» adds a **reply appendix on a new page after the dissent appendix**: the reply
  sections followed by the **signatures of the replying judges only**.
- Which judges reply is stored inside the copy's panel field values (`members[].replying` +
  `presidentReplying`); the reply text is a separate content column (`RebuttalSectionsJson`). When no
  dissent exists the reply flags are cleared on save. Both are **backward-compatible** — existing copies
  carry no reply.

### FR-21 — Auto-save draft recovery (JC-32, انقطاع الكهرباء)
Long forms (creating a request, preparing/correcting a copy) auto-save their in-progress state so a power
outage, tab crash, or expired session never loses unsent work. Drafts are **transient recovery data**,
scoped per user + form, and are **never** part of the legal record. See `docs/auto-save-draft.md`.
**Acceptance:**
- **Local save:** the form payload is written to `localStorage` (debounced ~500 ms) so it survives a
  reload / power loss.
- **Server sync:** the local draft is synced to the server (`FormDrafts` table) periodically
  (~10 s, configurable via `VITE_AUTO_SAVE_DRAFT_SYNC_INTERVAL_MS`) and on reconnect; last-write-wins is
  resolved by the **server clock** (`UpdatedUtc`).
- **Restore:** on return the newest of the local/server draft is offered — *«توجد مسودة محفوظة، هل تريد
  استرجاعها؟»* — the decision is cached so it is not re-asked.
- **Scoping & authorization:** a draft is always keyed to `UserId` (a crafted form key cannot reach
  another user's row); a copy-tied draft is allowed only for the role that may edit that copy in its
  current state (assigned Copyist while editable; Reviewer while under review).
- **Size guard:** payload is capped at **256 KB** — enforced in the entity, the service, and a DB
  `CHECK` constraint.
- **Local-copy deletion is event-driven** (NOT scheduled): cleared on submit/approve, on decline-restore,
  and — for shared-machine confidentiality — on **logout** (all local drafts wiped).
- **Server-copy cleanup is scheduled:** a lightweight background service (`PeriodicTimer`, no external job
  framework) deletes drafts older than **30 days** (`FormDraftCleanup:{Enabled,OlderThanDays,IntervalHours}`).

## 8. Non-functional requirements

### Security
- JWT authentication
- Role-based and permission-based access control (enforced server-side)
- Password hashing
- Audit logging

### Audit
Tracked actions: create, accept, edit, submit, return, approve, unlock, delete, expedite. Each entry captures actor,
timestamp, and before/after values where content changes. **Audit history is append-only
and is never deleted** — even when a copy is deleted (FR-16), its audit trail is retained.

### Localization
The interface is Arabic and must render correctly right-to-left (layout, alignment, form
fields, and reports). Arabic text is stored as Unicode with an appropriate collation.

### Performance & availability *(proposed — values to be set)*
**[OPEN]** Define targets for response time, concurrent users, expected document volume,
availability, and backup/retention, given this is a Ministry of Justice system with
permanent audit requirements.

## 9. Data retention *(proposed)*
**[OPEN]** Define retention and archival policy for copy records (distinct from the
permanent audit log).

## 10. Business rules

| ID | Rule |
|----|------|
| BR-01 | Only Registry Heads may create copy requests. |
| BR-02 | Only the assigned Copyist may edit an assigned copy. |
| BR-03 | Only Reviewers may approve copies. |
| BR-04 | Approved copies become read-only. |
| BR-05 | Only Administrators may unlock approved copies. |
| BR-06 | Users may only access their assigned scope, by role: **Copyists and Reviewers are scoped to assigned ROOMS** (one or more rooms, which may span several courts; their court scope is derived from those rooms), **Registry Heads to assigned COURTS**, and **Administrators are unrestricted**. Enforced server-side on every action/list/report (`Guard.RequireCopyScope`), never a UI hide. Room assignments live in `UserRoom`; court assignments in `UserCourt`. |
| BR-07 | Sequential copy numbers must be unique (scope per FR-06 [OPEN]). |
| BR-08 | A Reviewer may correct content directly while a copy is *Under review* (in addition to returning it); such edits are audited (actor = Reviewer) and do not change the state. This is the only content-write exception to BR-02. |
| BR-09 | A Registry Head may delete (via the deletion window) within their courts (FR-16; any state, incl. Approved): a **عادي** copy only if it is the court+year latest AND has **no linked متفرق** (else it would orphan them); or a **متفرق** only if it is the last in its numbering scope. Copy + content removed; audit retained + `delete` entry appended; the relevant counter (رقم النسخة for عادي, رقم المتفرق for متفرق) is rolled back — no gap. |
| BR-10 | Work-queue execution priority by الحالة: موقوف > مستعجل > عادي (default). مستعجل requires an expedite-request number. |
| BR-11 | A متفرق copy is **based on an Approved عادي copy** (النسخة الأصلية) and is **linked** to it: it gets **no رقم النسخة**, only an auto **رقم المتفرق** (by the room's numbering policy — court / room / special level A–Z **per court**, reset yearly), and **inherits** the original's court/room/رقم الأساس. رقم المرجع is **optional**. One original may have many linked متفرق copies. |
| BR-12 | رقم الأساس is **unique per court for عادي copies** (متفرق inherit the original's and are excluded). تاريخ الحجز is **server-assigned** at creation (not editable). |
| BR-13 | The Copyist must **accept** a copy before editing/submitting it; acceptance follows a **strict order** — priority tier (موقوف > مستعجل > عادي) then **oldest-first** within a tier (no skipping) — and its timestamp is recorded. The **Reviewer's approval** follows the **same strict order** (a copy cannot be approved while a higher-ranked copy is still under review in the reviewer's courts). A **non-approved** copy may be escalated to **مستعجل** at any time by the Registry Head (expedite number required), raising its priority. A **non-approved** copy may also be escalated from **عادي** or **مستعجل** to **موقوف** at any time by the Registry Head (an optional note — رقم طلب التصعيد — may be captured); موقوف is the highest priority and cannot be downgraded through the escalation flow. |
| BR-14 | Names are unique: **court name** and **judge name** are unique globally; **room name** is unique within its court. |

## 11. Open decisions summary

| # | Decision | Affects |
|---|----------|---------|
| 1 | Copy-number format and uniqueness scope | **RESOLVED** — per-court-per-year, `{courtCode}/{year}/{seq}` |
| 2 | Return-path target state and cycle limit | Target resolved (→ In preparation); **cycle cap open** |
| 3 | Post-unlock state and editor | **RESOLVED** — assigned Copyist re-edits, re-submits to Reviewer |
| 4 | Registry Head ↔ court scoping | FR-05, BR-06 |
| 5 | Cancellation / delete path | **RESOLVED** — Registry Head hard-deletes the latest عادي per court (blocked if it has linked متفرق) or the last متفرق per scope; relevant counter rolled back (no gap), audit retained (FR-16, BR-09/BR-11) |
| 6 | Report set (date/status/turnaround) | FR-13 |
| 7 | Notifications in scope and channel | FR-14 |
| 8 | Performance, availability, retention targets | NFR §8–9 |
