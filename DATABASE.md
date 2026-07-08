# JCS — Database schema (شرح جداول قاعدة البيانات)

SQL Server, EF Core 9 (code-first). Database default **collation `Arabic_CI_AS`** (case-insensitive,
accent-sensitive) for Arabic legal text. All text columns are Unicode (`nvarchar`). Schema changes
ship only as reviewed migrations (`src/ResourceIQ.Jcs.Infrastructure/Persistence/Migrations`).

> Quick answer — **numbering is stored in `CourtCopyCounters` (رقم النسخة) and `MiscNumberCounters`
> (رقم المتفرق)**; the per-room numbering *policy* lives on `Rooms`.

Legend: 🔑 primary key · 🔗 foreign key · ⭐ unique index · 📇 non-unique index.

---

## 1. Reference / configuration tables

### `Courts` — المحاكم (FR-03)
| Column | Type | Notes |
|--------|------|-------|
| Id 🔑 | uniqueidentifier | |
| Code ⭐ | nvarchar(50) | court code, **immutable** (embedded in copy numbers, e.g. `2/2026/0001`) |
| Name ⭐ | nvarchar(300) | unique globally (BR-14) |
| IsActive | bit | |

### `Rooms` — الغرف (FR-03)
A court is a set of rooms; a copy request targets one room; judges are assigned to rooms.
| Column | Type | Notes |
|--------|------|-------|
| Id 🔑 | uniqueidentifier | |
| CourtId 🔗📇 | uniqueidentifier | → `Courts` |
| Code | nvarchar(50) | **unique within the court** → composite ⭐ `(CourtId, Code)` |
| Name | nvarchar(300) | **unique within the court** → composite ⭐ `(CourtId, Name)` (BR-14) |
| IsActive | bit | |
| **NumberingPolicy** | int | رقم المتفرق scope: 1 = Court level, 2 = Room level, 3 = Special level (FR-03) |
| **NumberingLevel** | nvarchar(1) | special level letter A–Z — only when NumberingPolicy = Special; null otherwise |

### `Judges` — القضاة (FR-04)
| Column | Type | Notes |
|--------|------|-------|
| Id 🔑 | uniqueidentifier | |
| Name ⭐ | nvarchar(300) | unique globally (BR-14) |
| IsActive | bit | |

### `Users` — المستخدمون (FR-02)
| Column | Type | Notes |
|--------|------|-------|
| Id 🔑 | uniqueidentifier | |
| Username ⭐ | nvarchar(150) | |
| DisplayName | nvarchar(200) | |
| PasswordHash | nvarchar(max) | hashed; plaintext is never stored/logged |
| Role | int | 1 Administrator · 2 RegistryHead · 3 Copyist · 4 Reviewer |
| IsActive | bit | |

### `FormTemplates` / `FormFields` — النماذج الديناميكية (FR-08)
`FormTemplates`: Id 🔑, Name nvarchar(200), IsActive bit.
`FormFields`: Id 🔑, FormTemplateId 🔗 (→ FormTemplates), Key nvarchar(100), Label nvarchar(300),
Type nvarchar(50), ValidationRulesJson nvarchar(max) null, Order int. Rendered dynamically.

### `ParagraphTemplates` — الفقرات (FR-09)
Id 🔑, Title nvarchar(300), Body nvarchar(max), IsArchived bit, FormTemplateId 🔗📇 (→ FormTemplates,
null = global). Only non-archived paragraphs are insertable.

---

## 2. Association tables (many-to-many)

### `UserCourt` — تخصيص المحاكم للمستخدمين (FR-05, BR-06)
🔑 composite `(UserId, CourtId)`; 🔗 `Courts`. Drives court-scoping (a user sees only their courts).

### `JudgeRoom` — إسناد القضاة للغرف (FR-04)
🔑 composite `(JudgeId, RoomId)`; 🔗 `Rooms`. The judging panel is picked from a room's judges.

---

## 3. Workflow tables

### `CopyRequests` — النُسخ/القرارات (core aggregate, FR-06)
| Column | Type | Notes |
|--------|------|-------|
| Id 🔑 | uniqueidentifier | |
| CopyNumber | nvarchar(60) null | رقم النسخة `{courtCode}/{year}/{seq}` for **عادي**; **null for متفرق** (BR-11). ⭐ composite `(CourtId, CopyNumber)` — filtered, so NULLs are allowed |
| OriginalCopyId 🔗📇 | uniqueidentifier null | BR-11: for **متفرق**, the Approved عادي copy it is based on (self-FK, NoAction). null for عادي |
| CourtId 🔗📇 | uniqueidentifier | → `Courts` (cascade) |
| RoomId 🔗 | uniqueidentifier | → `Rooms` (NoAction — avoids multiple cascade paths) |
| CaseFilingDate | date null | قيد الدعوى (optional) |
| CaseBaseNumber | nvarchar(100) | رقم الأساس (required). ⭐ filtered unique `(CourtId, CaseBaseNumber, ReservationYear) WHERE [Category]=1` — unique per court **per ReservationDate.Year** for **عادي** only (BR-12, JC-22) |
| ReservationDate | date | تاريخ الحجز — **server-assigned at creation** (not editable); its year drives both numbering sequences |
| ReservationYear | int (computed, persisted) | `DATEPART(year, ReservationDate)` — not settable; exists only so SQL Server can index the year (BR-12/JC-22) |
| Category | int | التصنيف: 1 عادي · 3 متفرق |
| Urgency | int | الحالة: 1 موقوف · 2 مستعجل · 3 عادي |
| ExpediteRequestNumber | nvarchar(100) null | رقم طلب الاستعجال — required when Urgency = مستعجل |
| ReferenceNumber | nvarchar(100) null | رقم المرجع — required when Category = متفرق |
| **MiscNumber** | int null | رقم المتفرق — auto-allocated for متفرق copies (their only number) |
| State | int | 1 Created · 2 InPreparation · 3 UnderReview · 4 Approved · 5 Unlocked |
| AssignedCopyistId 📇 | uniqueidentifier null | the assigned copyist |
| AcceptedUtc | datetimeoffset null | FR-07: when the Copyist accepted the copy (before editing). Feeds the time-to-acceptance report |
| AcceptedById | uniqueidentifier null | the copyist who accepted |
| CreatedById | uniqueidentifier | Registry Head who created it |
| CreatedUtc 📇 | datetimeoffset | |
| UpdatedUtc | datetimeoffset null | |
| ApprovedUtc | datetimeoffset null | |
| ApprovedById 📇 | uniqueidentifier null | reviewer who approved |
| State 📇 | (index) | reporting |

A **متفرق** row has `CopyNumber = null`, `MiscNumber` set, and `OriginalCopyId` → its original عادي copy
(BR-11); it does not consume the رقم النسخة sequence. Deleting a row (FR-16) **cascades** to its
`CopyContents` but **never** to `AuditEntries`.

### `CopyContents` — محتوى النسخة (1‑to‑1 with CopyRequest)
| Column | Type | Notes |
|--------|------|-------|
| Id 🔑 | uniqueidentifier | |
| CopyRequestId 🔗 | uniqueidentifier | → `CopyRequests` (cascade delete) |
| FormTemplateId 🔗 | uniqueidentifier null | → `FormTemplates` |
| FieldValuesJson | nvarchar(max) | JSON of fixed-field values (panel, dates, …) |
| SectionsJson | nvarchar(max) | JSON array of inserted paragraph sections |
| Body | nvarchar(max) | legacy body text |

---

## 4. Numbering tables (الترقيم التسلسلي)

Both reset per year (the year = the copy's `ReservationDate.Year`); see FR-17/FR-18.

### `CourtCopyCounters` — عدّاد رقم النسخة
| Column | Type | Notes |
|--------|------|-------|
| CourtId 🔑🔗 | uniqueidentifier | → `Courts` |
| Year 🔑 | int | composite PK `(CourtId, Year)` |
| LastNumber | int | last سيريال issued; next copy = `LastNumber + 1` |

### `MiscNumberCounters` — عدّاد رقم المتفرق
| Column | Type | Notes |
|--------|------|-------|
| ScopeKey 🔑 | nvarchar(80) | scope: `C:{courtId}` (court level) · `R:{roomId}` (room level) · `S:{courtId}:{level}` (special level, per court) |
| Year 🔑 | int | composite PK `(ScopeKey, Year)` |
| LastNumber | int | last رقم متفرق issued; next = `LastNumber + 1` |

Allocation is atomic inside the create transaction (UPDATE+INSERT). Admins seed start points at
go-live (FR-17), guarded so a value can never be set below the highest number already used. There is
also an unused SQL `SEQUENCE CopyNumberSequence` (reserved for a global-numbering alternative; not active).

#### What happens to the counters when a decision is deleted (FR-16, BR-09/BR-11)

Deletion is by the **Registry Head**, within their courts, in two cases. The whole operation runs in a
single transaction (`DeleteCopyService`); in both, a `Delete` audit entry is appended (**kept forever**),
the `CopyRequests` row is removed (its `CopyContents` **cascades**; `AuditEntries` **untouched**), and the
relevant counter row is **NOT deleted** but its `LastNumber` is **decremented by 1**
(`UPDATE … SET LastNumber = LastNumber - 1 … AND LastNumber > 0`):

- **عادي** — must be the **court+year latest** رقم النسخة **and have NO linked متفرق** (else the delete is
  rejected, to avoid orphaning them). Rolls back **`CourtCopyCounters`** (court+year).
- **متفرق** — must be the **last رقم المتفرق in its numbering scope**. Rolls back **`MiscNumberCounters`**
  (scope+year). The متفرق has no رقم النسخة, so `CourtCopyCounters` is untouched.

The decrement (`ReleaseAsync`) frees the number so the **next** created copy reuses it → **no gap**.
Example: a counter at `LastNumber = 5` becomes `4`; the next is `5` again. If `LastNumber` was `1` it
becomes `0` (the row stays, never negative). If the guard fails the delete is rejected and **the counters
stay unchanged**.

---

## 5. Audit table (append-only)

### `AuditEntries` — سجلّ التدقيق 
| Column | Type | Notes |
|--------|------|-------|
| Id 🔑 | uniqueidentifier | |
| CopyRequestId 📇 | uniqueidentifier | the copy it concerns — **plain column, NO FK / NO cascade** |
| ActorId | uniqueidentifier | acting user |
| ActorName | nvarchar(200) | |
| Action | int | 1 Create · 2 Edit · 3 Submit · 4 Return · 5 Approve · 6 Unlock · 7 Delete · 8 Accept · 9 Expedite |
| TimestampUtc | datetimeoffset | |
| BeforeJson / AfterJson | nvarchar(max) null | content snapshots where applicable |
| Reason | nvarchar(2000) null | mandatory for unlock/return |

**Append-only**: no UPDATE/DELETE path in code; deleting a copy keeps its audit. In production,
revoke UPDATE/DELETE on this table for the app's SQL login (see `DEPLOY.md`).
