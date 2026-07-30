# JCS — Developer Onboarding & Handover Guide

Welcome to the **Judicial Copying System (JCS)** — the system that digitizes the decision-copying
workflow of the Court of Cassation (محكمة النقض) at the Ministry of Justice.

This guide is written for **developers who are new to the project** (including recent graduates). It
explains *how the codebase is organized and why*, and gives **step-by-step recipes** for the everyday
tasks you will do: adding a backend API, adding a new screen, modifying an existing screen, and
changing the database. It covers **both applications** — the **.NET backend** and the **React frontend**.

> Read this once end-to-end, then keep it open as a reference. When a rule here conflicts with what you
> think is "easier", follow the rule — this is a **legal, audited** system and correctness matters more
> than speed.

### Companion documents (read these too)
| File | What it gives you |
|------|-------------------|
| `PRD.md` | Product requirements — every feature (FR-xx) and business rule (BR-xx). The "what". |
| `BLUEPRINT.md` | Architecture overview, domain model, API surface, state machine. The "how", high level. |
| `WORKFLOW.md` | The copy lifecycle (states, transitions, who can do what). |
| `DATABASE.md` | Every table, column, key, and index. |
| `DEPLOY.md` | How to run the production Docker stack. |
| `Asd.md` | The **non-negotiable invariants** (audit append-only, approved=read-only, server-side authz, …). |

When you implement anything, the requirement it satisfies should trace back to an FR/BR in `PRD.md`.

---

## 1. The big picture

JCS is **two applications + one database**, plus an optional Docker deployment.

```
┌──────────────────────┐      JWT (Bearer)      ┌──────────────────────┐     EF Core 9      ┌────────────┐
│  Frontend (web/)     │  ───────────────────►  │  Backend API (src/)  │  ───────────────►  │ SQL Server │
│  React 19 + Vite + TS│  ◄───────────────────  │  .NET 9 / C#         │  ◄───────────────  │  (Jcs DB)  │
│  RTL, Arabic UI      │      JSON over HTTPS    │  layered architecture│                    │ Arabic_CI_AS│
└──────────────────────┘                        └──────────────────────┘                    └────────────┘
        served by Nginx (prod), which also reverse-proxies /api → the API container
```

- The **frontend** is a Single-Page Application (SPA). It renders the UI and talks to the backend over
  HTTP. It **never** trusts itself for security — every rule is re-checked on the server.
- The **backend** is the source of truth: it enforces roles, generates sequential numbers, locks
  approved copies, and writes the permanent audit trail.
- The **database** stores everything. Arabic text uses Unicode (`nvarchar`) with the `Arabic_CI_AS`
  collation.

There are **two ways to run it**:
1. **Local development** — backend on `http://localhost:5253`, frontend (Vite dev server) on
   `http://localhost:5173`, database on SQL Server LocalDB. Fast feedback, hot reload.
2. **Production-style Docker** — three containers (SQL Server + API + Nginx/SPA) via `docker compose`.
   See `DEPLOY.md`.

---

## 2. Getting started (run it locally first)

You learn a codebase fastest by running it and clicking around.

### Prerequisites
- **.NET 9 SDK** (`dotnet --version` → 9.x)
- **Node.js 20+** and npm (`node --version`)
- **SQL Server LocalDB** (ships with Visual Studio / the SQL Server Express installer)
- Optional: **Docker Desktop** (only for the production-style run)

### Build it locally

Before you *run* anything, make sure both apps **build cleanly**. Building only compiles the code — it
does not start a server (that is the next step). The backend and frontend build independently.

**Backend (.NET solution):** from the repo root —
```bash
dotnet build                 # builds the whole solution: Domain → Application → Infrastructure → Api + tests
dotnet build -c Release      # optional: a Release (optimized) build instead of the default Debug
```
`dotnet build` restores NuGet packages on first run and compiles all projects. Compiled output lands in
each project's `bin/` folder (e.g. `src/ResourceIQ.Jcs.Api/bin/`). A red error here is a **compile
error** — fix it before running.

**Frontend (React + Vite):** from the `web/` folder —
```bash
cd web
npm install        # first time only — restores node_modules
npm run build      # runs `tsc -b` (type-check) then `vite build`; static output → web/dist/
```
`npm run build` **fails on any TypeScript error**, so it doubles as the frontend's safety net (there is
no unit-test suite yet — see §14). The bundled static site is written to `web/dist/`.

**Build everything in one pass** (copy-paste from the repo root) —
```bash
dotnet build && cd web && npm install && npm run build && cd ..
```
If this completes with no errors, both apps compile and you are ready to **run them** (next section).

> **Note:** *build* ≠ *run*. A clean build does not mean the app is running — see "Run the backend /
> frontend" below. If `dotnet build` fails with a **"DLL is locked"** error, the API is still running
> from a previous session; stop it first (see §17, Common pitfalls).

### Run the backend
```bash
# from the repo root
dotnet build                                   # builds the whole solution
dotnet run --project src/ResourceIQ.Jcs.Api    # starts the API on http://localhost:5253
```
In **Development**, the API automatically applies EF migrations and seeds reference data on startup.
(In Production this only happens behind the explicit `JCS_BOOTSTRAP` flag — never automatically.)

### Run the frontend
```bash
cd web
npm install        # first time only
npm run dev        # starts Vite on http://localhost:5173
```
Open `http://localhost:5173`, click **تسجيل الدخول** (Login), and sign in. The Vite dev server
proxies `/api/*` to the backend (see `web/vite.config.ts`), so the SPA and API behave as same-origin.

### Run the production stack (Docker)
```bash
# set values in .env (see .env.example), then:
docker compose up -d --build
docker compose logs -f api      # watch the API come up
```
Details and the one-time bootstrap procedure are in `DEPLOY.md`.

---

## 3. Repository map

```
JCS solution/
├─ src/                         ← BACKEND (.NET solution)
│  ├─ ResourceIQ.Jcs.Domain/         Entities, enums, domain rules, state machine (no dependencies)
│  ├─ ResourceIQ.Jcs.Application/    Use-cases (services), DTOs, abstractions (interfaces)
│  ├─ ResourceIQ.Jcs.Infrastructure/ EF Core, repositories, query implementations, allocators, audit
│  └─ ResourceIQ.Jcs.Api/            ASP.NET Core: controllers, auth, middleware, PDF, startup
├─ tests/
│  └─ ResourceIQ.Jcs.Tests/          xUnit unit tests (+ in-memory "fakes")
├─ web/                          ← FRONTEND (React + Vite + TypeScript)
│  └─ src/
│     ├─ api/client.ts               Typed fetch wrapper + all server types + the `api` object
│     ├─ app/                        nav.tsx (router), AppLayout.tsx (shell), ui.tsx (shared UI), app.css
│     ├─ auth/AuthContext.tsx        Login state + token
│     ├─ components/                 Reusable building blocks (Emblem, ConnectionStatus, SiteHeader…)
│     ├─ features/                   One folder per feature area (requests, admin, reports, print…)
│     └─ i18n.tsx                    Arabic/English language switch
├─ PRD.md, BLUEPRINT.md, WORKFLOW.md, DATABASE.md, DEPLOY.md, Asd.md
└─ docker-compose.yml, Dockerfile, web/Dockerfile, .env(.example)
```

---

# PART A — The Backend (.NET)

## 4. The architecture: layered / "Clean Architecture"

The backend is split into **four projects** that form layers. The golden rule is the **dependency
direction**: dependencies point *inward*. Inner layers know nothing about outer layers.

```
        Api  ──►  Application  ──►  Domain
         │            │
         └────────────┴────────►  Infrastructure  ──► (implements Application's interfaces)
```

| Project | Responsibility | Depends on | Examples |
|---------|----------------|-----------|----------|
| **Domain** | The business model and rules. Pure C#, no framework, no database. | nothing | `CopyRequest` (the aggregate), enums (`CopyState`, `CaseUrgency`), `CopyStateMachine`, `DomainException`. |
| **Application** | Use-cases ("what the system does"). One **service** per workflow action. Defines **interfaces** (abstractions) for anything it needs from the outside (DB, clock, hashing). | Domain | `CreateCopyRequestService`, `ReviewService`, `IJcsQueries`, `ICopyRequestRepository`, DTOs in `ReadModels`. |
| **Infrastructure** | The concrete "how": EF Core DbContext, repository/query implementations, number allocators, audit writer, JWT, password hashing, clock. **Implements** the Application's interfaces. | Application, Domain | `JcsDbContext`, `CopyRequestRepository`, `JcsQueries`, `PerCourtCopyNumberAllocator`. |
| **Api** | The HTTP edge: controllers, request DTOs, JWT setup, middleware, PDF rendering, startup wiring. | Application, Infrastructure | `CopyRequestsController`, `Program.cs`, `Contracts/Dtos.cs`. |

**Why this matters to you:** business rules live in **Domain/Application**, not in controllers. A
controller should be thin — validate the shape of the request, call a service, return the result.
If you find yourself writing an `if (user.Role == …)` or a DB query inside a controller, stop: that
logic belongs in a service (authorization) or a query (data).

### How the layers are wired (Dependency Injection)
ASP.NET Core uses a built-in DI container. Each layer exposes one registration method:
- `src/ResourceIQ.Jcs.Application/DependencyInjection.cs` → `AddJcsApplication()` registers the
  services (`CreateCopyRequestService`, `ReviewService`, …).
- `src/ResourceIQ.Jcs.Infrastructure/DependencyInjection.cs` → `AddJcsInfrastructure(config)`
  registers the DbContext and binds every interface to its implementation
  (`ICopyRequestRepository → CopyRequestRepository`, `IClock → SystemClock`, …).
- `src/ResourceIQ.Jcs.Api/Program.cs` calls both, then adds API-only things (JWT, the PDF service,
  `ICurrentUser`).

When a controller's constructor asks for `CreateCopyRequestService`, the container builds it and all
of its dependencies automatically. **You almost never `new` a service yourself.**

## 5. The request lifecycle (follow one request end-to-end)

Take "**Registry Head creates a copy request**" (FR-06). Trace it:

1. **HTTP** `POST /api/copy-requests` arrives with a JSON body.
2. **Controller** `CopyRequestsController.Create` (in `src/.../Api/Controllers`) binds the body to a
   **request DTO** (`CreateCopyRequestRequest` in `Api/Contracts/Dtos.cs`), maps it to an
   **application command** (`CreateCopyRequestCommand`), and calls `createService.HandleAsync(...)`.
3. **Service** `CreateCopyRequestService` (Application layer):
   - Checks authorization with `Guard.RequireRole(...)` / `Guard.RequireAssignedCourt(...)`.
   - Opens a transaction (`IUnitOfWork`), creates the domain entity via the **factory**
     `CopyRequest.Create(...)`, allocates the sequential number via `ICopyNumberAllocator`, writes an
     audit entry via `IAuditWriter`, and saves.
4. **Domain** `CopyRequest.Create(...)` enforces invariants (e.g. مستعجل requires an expedite number).
   State changes only go through methods like `AssignToCopyist`, `SubmitForReview`, `Approve` — each
   validates the transition against `CopyStateMachine`.
5. **Infrastructure** the repository/DbContext translate all of that into SQL against SQL Server.
6. The controller returns `201 Created` with the new id + copy number.

**Reads** are separate from writes: list/detail/report queries go through **query interfaces**
(`IJcsQueries`, `IReportQueries`) that return **DTOs** directly (no domain entities leak out). This is
a light **CQRS** style — commands mutate via services+repositories, queries read via query objects.

### Key building blocks you will use constantly
| Concept | Where | What it is |
|--------|-------|-----------|
| **Aggregate / Entity** | `Domain/Entities/CopyRequest.cs` | The core object. All state changes are methods on it; setters are `private`. |
| **State machine** | `Domain/Workflow/CopyStateMachine.cs` | The only place that says which state→state transitions are legal. |
| **DomainException** | `Domain/Rules` | Throw this for a broken business rule; the API turns it into a clean 4xx (see middleware). |
| **Service** | `Application/<area>/<Name>Service.cs` | One use-case. Orchestrates domain + repos + audit in a transaction. |
| **Command** | next to the service (a `record`) | The typed input to a service method. |
| **DTO** | `Application/ReadModels/ReadDtos.cs` | Read-side output shapes returned to the API. |
| **Abstraction (interface)** | `Application/Abstractions/*` and `Application/Security/ICurrentUser` | What the Application needs from the world; implemented in Infrastructure. |
| **Repository** | `Infrastructure/Persistence/CopyRequestRepository.cs` | Load/add/remove entities for the write side. |
| **Query** | `Infrastructure/Persistence/JcsQueries.cs` | Read-only EF projections → DTOs. |
| **EF configuration** | `Infrastructure/Persistence/Configurations/ModelConfigurations.cs` | Maps entities to tables/columns/indexes. |
| **Guard** | `Application/Security/Guard.cs` | Helper for `RequireRole` / `RequireAssignedCourt` / `RequireAuthenticated`. |
| **ICurrentUser** | resolved from the JWT per request | The authenticated caller (id, role, assigned courts). Server-trusted. |

## 6. Backend conventions (do these every time)
- **Async everywhere** for I/O: `async`/`await`, methods end in `Async`, accept a `CancellationToken`.
- **Validate at the boundary, enforce rules in the domain/service.** Controllers check request shape;
  services check authorization and business rules; the domain protects its own invariants.
- **Authorization is server-side, always.** Never rely on the client having hidden a button.
- **Wrap multi-step writes in a transaction** (`IUnitOfWork.ExecuteInTransactionAsync`) so they commit
  all-or-nothing (number allocation + audit + state change must not partially apply).
- **Never log secrets or full legal content** at info level. Never store plaintext passwords.
- **Follow the existing namespaces/folders.** New copy-request use-case → `Application/CopyRequests/`.

---

## 7. HOW TO: add a new backend API endpoint

We'll add a fictional endpoint as a worked example: **"GET the count of copies a copyist still needs
to accept"** (read-only). Adapt the steps for writes.

> Mental model: **DTO/command → interface → implementation → service → controller → DI (if new type)**.
> Work from the inside out.

**Step 1 — Decide: is it a read or a write?**
- *Read* → add a method to a **query** interface (`IJcsQueries` / `IReportQueries`) and implement it in
  `JcsQueries` / `ReportQueries`. Return a **DTO**.
- *Write* → add a **command** + a **service** (or a method on an existing service), and a
  **repository** method if you need to load/modify entities.

**Step 2 — (Read) Add the DTO** in `Application/ReadModels/ReadDtos.cs`:
```csharp
public sealed record PendingAcceptanceDto(Guid CopyistId, int Pending);
```

**Step 3 — Declare it on the query interface** `Application/Abstractions/IJcsQueries.cs`:
```csharp
Task<PendingAcceptanceDto> GetPendingAcceptanceAsync(Guid copyistId, CancellationToken ct);
```

**Step 4 — Implement it** in `Infrastructure/Persistence/JcsQueries.cs` (EF projection, `AsNoTracking`):
```csharp
public async Task<PendingAcceptanceDto> GetPendingAcceptanceAsync(Guid copyistId, CancellationToken ct)
{
    var pending = await db.CopyRequests.AsNoTracking()
        .CountAsync(x => x.AssignedCopyistId == copyistId
                      && x.State == CopyState.InPreparation && x.AcceptedUtc == null, ct);
    return new PendingAcceptanceDto(copyistId, pending);
}
```
> If you add a method to `IJcsQueries`, you must also implement it in the **test fake**
> `tests/ResourceIQ.Jcs.Tests/Fakes.cs` (otherwise the test project won't compile).

**Step 5 — Expose it through a service** (so authorization is enforced). For a copyist-scoped read,
put it on `CopyRequestReadService` (`Application/CopyRequests/`):
```csharp
public Task<PendingAcceptanceDto> GetMyPendingAcceptanceAsync(CancellationToken ct)
{
    Guard.RequireRole(currentUser, Role.Copyist);          // server-side authz
    return queries.GetPendingAcceptanceAsync(currentUser.Id, ct);
}
```

**Step 6 — Add the controller action** in `Api/Controllers/CopyRequestsController.cs`:
```csharp
[HttpGet("pending-acceptance")]
public async Task<IActionResult> PendingAcceptance(CancellationToken ct) =>
    Ok(await readService.GetMyPendingAcceptanceAsync(ct));
```
The controller is **thin**: no business logic, no DB. `[Authorize]` is already on the controller, so a
valid JWT is required; the *role* check happens in the service.

**Step 7 — Register DI only if you introduced a NEW service/type.** Reusing an existing service
(as above) needs no DI change. A brand-new `FooService` must be added to
`Application/DependencyInjection.cs` (`services.AddScoped<FooService>()`); a new interface+impl pair
goes in `Infrastructure/DependencyInjection.cs`.

**Step 8 — For a WRITE endpoint**, additionally:
- Add a `record FooCommand(...)` next to the service.
- Add the state-changing method to the **domain entity** (validate the transition via
  `CopyStateMachine`), not in the service.
- In the service: `Guard.Require…`, then `unitOfWork.ExecuteInTransactionAsync(async token => { … load, mutate, audit.Append(...), SaveChangesAsync; })`.
- Add a request DTO in `Api/Contracts/Dtos.cs` and map it in the controller.
- See `AcceptCopyService` / `ExpediteCopyService` as small, complete real examples.

**Step 9 — Build, test, and mirror the type on the frontend** (`web/src/api/client.ts`, see Part B).
```bash
dotnet build
dotnet test tests/ResourceIQ.Jcs.Tests/ResourceIQ.Jcs.Tests.csproj
```

### HOW TO: add a new entity + table
1. Add the class in `Domain/Entities/` (private setters; a `Create` factory if it has invariants).
2. Add a `DbSet<T>` to `JcsDbContext` and an `IEntityTypeConfiguration<T>` in
   `Persistence/Configurations/ModelConfigurations.cs` (lengths, indexes, relationships).
3. Create a migration and review the generated SQL **before** applying:
   ```bash
   dotnet ef migrations add AddFooTable -p src/ResourceIQ.Jcs.Infrastructure -s src/ResourceIQ.Jcs.Api
   ```
4. In Development the API applies it on startup. Never hand-edit the database; never auto-apply in
   production (that is gated by `JCS_BOOTSTRAP`).

### HOW TO: add a field to an existing entity
Add the property (private setter) → add it to the EF config if it needs constraints/index → add a
migration → surface it in the relevant DTO + query projection → mirror it in the frontend type.
(We did exactly this for `AcceptedUtc` and `OriginalCopyId` — search the repo for those to see the
full set of files a single field touches.)

---

# PART B — The Frontend (React + Vite + TypeScript)

## 8. Frontend philosophy: minimal dependencies, hand-rolled core

This SPA is **deliberately minimal**. The only runtime libraries are `chart.js` + `react-chartjs-2`
(report charts) and `qrcode.react` (printed-copy QR). There is **no react-router, no Redux, no
react-query, no UI/CSS framework**. Routing, data-fetching, i18n, and shared UI are **hand-rolled** in
`web/src/app/`. Before adding any dependency, **flag it** — the bar is high on purpose.

The UI is **Arabic and right-to-left (RTL) first**. Styling is plain CSS using *logical properties*
(`margin-inline-start`, `border-inline-end`, …) so it mirrors correctly under RTL.

### The four core utilities (learn these first)
| File | Role | Key exports |
|------|------|-------------|
| `src/api/client.ts` | The **only** place that talks HTTP. A typed `request<T>()` wrapper + all server-mirrored TypeScript types + an `api` object grouping every endpoint. | `api`, `setToken`, types like `CopyRequestListItem`. |
| `src/app/nav.tsx` | A tiny **in-memory router** (no URLs). A "route" is `{ page, id? }` kept in React state. | `NavProvider`, `useNav()` → `{ route, navigate }`. |
| `src/app/AppLayout.tsx` | The authenticated **shell**: the role-based nav menu (`navByRole`) and the `Outlet` that `switch`es on `route.page` to render the right feature page. | `AppLayout`. |
| `src/app/ui.tsx` | Shared presentational components and label maps. | `useL` (i18n helper), `StateBadge`, `Spinner`, `ErrorBox`, `Modal`, `useSort`, `SortTh`, `categoryLabels`, `urgencyLabels`, `auditLabels`, `roleLabels`. |

Plus: `src/auth/AuthContext.tsx` (login/logout, current user, sets the token on `client.ts`),
`src/i18n.tsx` (Arabic⇄English), `src/Shell.tsx` (chooses between the public site and the app shell
based on auth).

### How a screen gets data (the standard pattern)
```tsx
const [items, setItems] = useState<CopyRequestListItem[] | null>(null);
const [err, setErr] = useState<string | null>(null);

useEffect(() => {
  api.listRequests({}).then(setItems).catch((e) => setErr(e.message));
}, []);
```
- `null` means "still loading" → render a `<Spinner/>`. An error → render `<ErrorBox/>`.
- All text uses `useL("عربي", "English")` so it works in both languages.
- Enums are **strings** on the wire (`"InPreparation"`, `"Expedited"`), matching the C# enum names.

### How routing works (no URLs!)
`navigate("request", id)` just sets React state; `AppLayout`'s `Outlet` re-renders the matching page.
Consequences a junior must remember:
- The browser address bar **does not change**; the **Back button and F5 won't restore a screen** (F5
  resets to the initial page). This is by design for this internal tool.
- To make a row clickable, attach `onClick={() => navigate("request", r.id)}`.

---

## 9. HOW TO: add a new screen (page)

Worked example: add an admin screen **"Pending acceptance per copyist"** that calls the endpoint from
Part A §7.

**Step 1 — Add the type + API method** in `src/api/client.ts` (mirror the server DTO exactly; enums
are string unions):
```ts
export interface PendingAcceptance { copyistId: string; pending: number; }

// inside the `api` object:
myPendingAcceptance: () => request<PendingAcceptance>("/api/copy-requests/pending-acceptance"),
```
> Keep `client.ts` the single source of truth for server shapes. Components import types from here;
> they never hand-write `fetch`.

**Step 2 — Create the feature component** under the right `features/<area>/` folder, e.g.
`src/features/requests/PendingAcceptancePage.tsx`:
```tsx
import { useEffect, useState } from "react";
import { api, type PendingAcceptance } from "../../api/client";
import { useL, Spinner, ErrorBox } from "../../app/ui";

export function PendingAcceptancePage() {
  const L = useL();
  const [data, setData] = useState<PendingAcceptance | null>(null);
  const [err, setErr] = useState<string | null>(null);
  useEffect(() => { api.myPendingAcceptance().then(setData).catch((e) => setErr(e.message)); }, []);
  if (err) return <ErrorBox message={err} />;
  if (!data) return <Spinner label={L("جارٍ التحميل…", "Loading…")} />;
  return (
    <>
      <h1 className="page-title">{L("بانتظار القبول", "Pending acceptance")}</h1>
      <p>{L("عدد القرارات", "Count")}: <strong>{data.pending}</strong></p>
    </>
  );
}
```
Reuse existing CSS classes (`page-title`, `card`, `table`, `btn`, `field`, …) — see `app/app.css`.

**Step 3 — Register the route + nav entry** in `src/app/AppLayout.tsx`:
- Import the page at the top: `import { PendingAcceptancePage } from "../features/requests/PendingAcceptancePage";`
- Add a nav item under the right role in `navByRole` (use a unique `page` key):
  ```ts
  { page: "pending-acceptance", ar: "بانتظار القبول", en: "Pending acceptance" },
  ```
- Add a `case` to the `Outlet` switch:
  ```tsx
  case "pending-acceptance": return <PendingAcceptancePage />;
  ```
That's it — the menu button appears for that role and clicking it renders your page.

**Step 4 — Build / type-check:**
```bash
cd web
npm run build      # runs `tsc -b` then `vite build` — fails on any type error
```

> If your page takes a parameter (like a record id), pass it through navigation
> (`navigate("request", id)`) and read it in the `Outlet` as `route.id` (see how `request`/`prepare`
> pages do it in `AppLayout.tsx`).

## 10. HOW TO: modify an existing screen

1. **Find the page.** Screens live in `src/features/<area>/`. Match the menu label to the file via
   `AppLayout.tsx` (`navByRole` → `Outlet` switch). Example: the requests list is
   `features/requests/RequestsListPage.tsx`.
2. **Add a column / field?** If the data already exists on the DTO, just render it. If not, add it on
   the **server DTO + query** first, then to the type in `client.ts`, then to the component.
3. **Add an action button?** Add the API method in `client.ts`, then a handler in the component that
   calls it and refreshes (`await api.foo(); reload();`). Show a `<Modal/>` for confirmations
   (see `DeletionOperationsPage.tsx`) and disable the button while busy.
4. **Respect RTL + i18n:** every new string goes through `useL("..","..")`; use logical CSS properties.
5. **Type-check** with `npm run build`.

### HOW TO: change a field label (rename a label on a screen)

There are **two kinds of labels**, and the way you change each one is different. First figure out which
kind you are looking at, then follow the matching recipe.

**Quick rule to tell them apart:**
- Text written as `L("النص العربي", "English text")` → **static**, changed **in the code** (Case 1).
- Text rendered from a value like `{fld.label}`, `{f.name}`, or anything coming from the API →
  **dynamic**, changed **from the Admin screen** (Case 2).

**Case 1 — Static UI label (hard-coded in the code via `L(...)`)**

These are the fixed labels and buttons of the interface — e.g. «نوع القرار» (Decision type), «رجوع»
(Back), «حفظ مسودة» (Save draft), and the screen buttons.

*How to recognize it:* it is written **literally** inside a `.tsx` file in the form
`L("Arabic text", "English text")`.

*How to change it:*
1. Search for the text (Grep) inside `web/src`.
2. Open the `.tsx` file and edit **both strings** inside `L(...)` — Arabic first, then English.
3. Rebuild / redeploy. Locally, Vite picks it up instantly via HMR.

*Example* — to change «نوع القرار» → open `PreparePage.tsx` (around line 230):
```tsx
<span>{L("نوع القرار", "Decision type")}</span>
```
There is **no central translation file**; each string lives in its own component (as `i18n.tsx` notes,
the strings will be moved into an i18n catalog in the future).

**Case 2 — Dynamic form-field label (comes from the database)**

These are the labels of the fields of a decision form — e.g. «رقم القرار» (Decision number), «الهيئة
الحاكمة» (Panel), «السنة» (Year), «تاريخ الإصدار» (Issue date)…

*How to recognize it:* it is rendered via `{fld.label}` (not a literal string). Its value is stored in
`FormTemplate.fields[].label` in the database.

*How to change it — two ways:*

**(a) From the Admin screen (preferred — no rebuild):**
Admin → «النماذج» (Forms) → edit the template → change the field's «التسمية» (label) box → save. It is
stored directly in the database and appears immediately for all users. The support for this is in
`FormsPage.tsx:91-92`.

**(b) In the seeder (for brand-new templates only):**
`DbSeeder.cs:283-289`, e.g. `F("decisionNumber", "رقم القرار", "text", 4)`. ⚠️ This affects **only**
templates that are created for the first time — it does **not** change templates that already exist in a
running database (seeding is idempotent). For existing templates, use method (a).

## 11. HOW TO: call a backend endpoint from the UI
Always go through `client.ts`:
- Add the typed method to the `api` object (grouped by area — e.g. `api.admin.*`, `api.reports.*`).
- Auth is automatic: after login, `AuthContext` calls `setToken(...)`, and `request<T>()` attaches the
  `Authorization: Bearer` header. You never handle the token in components.
- A non-2xx response throws an `Error` whose message is the server's `error` field — catch it and show
  an `<ErrorBox/>`.

---

## 12. The end-to-end picture: a feature that spans both apps

Most real features touch the database, backend, **and** frontend. The order that keeps you sane:

1. **Domain/DB first** — entity field + EF config + migration (Part A §7).
2. **Backend read/write** — query or command + service + controller (Part A §7). Build + unit test.
3. **Frontend type** — mirror the new shape in `client.ts`.
4. **Frontend UI** — render it / add the action (Part B §9–§10). `npm run build`.
5. **Verify end-to-end** — run both apps, click through the flow; check the audit trail and (for
   numbering changes) that no gaps/collisions appear.

`AcceptedUtc` (copyist acceptance, FR-07) and `OriginalCopyId` (متفرق linkage, BR-11) are recent
features you can read top-to-bottom as templates — grep the repo for either name to see *every* file a
cross-cutting change touches.

---

## 13. Cross-cutting rules you must never break 

These are **invariants**. Breaking one is a serious defect in a legal system.
1. **Audit is append-only and never deleted** — even when a copy is deleted, its audit rows stay.
   There is no update/delete path for `AuditEntries`.
2. **Approved copies are read-only** (BR-04). The only write to an approved copy is the Administrator
   *unlock* flow; the only delete is the Registry Head deletion window.
3. **Authorization is enforced server-side** on every action — re-checked against the caller's role and
   assigned courts (BR-06). The client is never trusted.
4. **Sequential numbers are generated server-side, atomically, inside the create transaction** — never
   in app memory, never on the client. Confirm uniqueness scope before touching it.
5. **Never silently drop, truncate, or auto-correct user-entered legal text.**
6. **Never log secrets/tokens/passwords or full copy content.**
7. **Migrations are deliberate and reviewed**; never auto-apply in production.

When a requirement is ambiguous or seems to need an undefined workflow rule, **ask** — do not guess.
Correctness and auditability outrank momentum.

---

## 14. Testing

- **Backend unit tests** live in `tests/ResourceIQ.Jcs.Tests/` (xUnit). They test the domain
  (`CopyRequestTests`, `CopyStateMachineTests`) and services (`WorkflowServiceTests`, `ReportServiceTests`)
  using **in-memory fakes** in `Fakes.cs` (fake repository, clock, allocators, queries) — no database
  needed. Run them:
  ```bash
  dotnet test tests/ResourceIQ.Jcs.Tests/ResourceIQ.Jcs.Tests.csproj
  ```
  When you add a method to an interface like `IJcsQueries` / `ICopyRequestRepository`, **update the
  matching fake** or the test project won't compile.
- **Frontend** has no unit-test suite yet; the safety net is the TypeScript compiler. Always run
  `npm run build` (it runs `tsc -b`) before you consider a change done.
- For workflow changes, also do a **manual end-to-end pass**: create → accept → prepare → submit →
  review/approve → print, and confirm the audit trail looks right.

## 15. Database & migrations workflow

### What "Code-First" means
In the **Code-First** pattern, your **C# code is the source of truth** for the database schema — not
the other way around. You write entity classes (`Domain/Entities/`) and their EF configurations
(`Persistence/Configurations/ModelConfigurations.cs`), and EF Core **generates the schema** from them.
You do **not** hand-write `CREATE TABLE` SQL, and you **never** edit the live database by hand.

A **migration** is one timestamped "change step". When you modify an entity (add a column, table,
index), EF compares the current shape of your code against the last recorded **snapshot** of the model
and generates a C# file describing how to move the database forward (`Up`) and how to roll it back
(`Down`). The chain of migrations is the full, reviewable history of the schema.

```
1. Edit the entity / EF configuration in C#
2. dotnet ef migrations add <DescriptiveName>   → generates the migration + updates the snapshot
3. Review the generated file (it is normal code review — read the SQL it will run)
4. Apply it (dev: on startup; prod: gated behind JCS_BOOTSTRAP)
```

### How it is applied in THIS project
- **Source of truth — `JcsDbContext`** (`Infrastructure/Persistence/JcsDbContext.cs`): declares every
  `DbSet<>`, sets the Arabic collation (`modelBuilder.UseCollation("Arabic_CI_AS")`), pulls in all
  entity configurations, and defines the copy-number `SEQUENCE`
  (`HasSequence<long>(CopyNumberSequence)`). Because the sequence is declared here, it becomes a real
  database object — which is exactly how copy numbers are allocated server-side and atomically (BR-07),
  not in app memory.
- **The migration chain** lives in `Infrastructure/Persistence/Migrations/`. It runs from
  `InitialCreate` through later feature steps (`PerCourtCopyNumbering`, `MiscNumberAndReferenceNumber`,
  `RoomNumberingPolicy`, `MiscOriginalCopyLink`, `UniqueNames`, …) — each one maps directly to a
  feature or business rule. Every migration has **three files**:

  | File | Purpose |
  |------|---------|
  | `<timestamp>_Name.cs` | The `Up`/`Down` operations — what gets applied, and how to undo it. |
  | `<timestamp>_Name.Designer.cs` | The model snapshot **at that point in time**. |
  | `JcsDbContextModelSnapshot.cs` | The **current cumulative** snapshot — the baseline EF diffs against to generate the next migration. |

- **A concrete `Up`/`Down` example** — `20260624160805_MiscNumberAndReferenceNumber.cs`:
  ```csharp
  protected override void Up(MigrationBuilder mb) {
      mb.AddColumn<int>("MiscNumber", "CopyRequests", nullable: true);
      mb.AddColumn<string>("ReferenceNumber", "CopyRequests", "nvarchar(100)", maxLength: 100, nullable: true);
      mb.CreateTable("MiscNumberCounters", /* … */ );   // PK = (ScopeKey, Year) → year-keyed counter (FR-18)
  }
  protected override void Down(MigrationBuilder mb) {     // every change is reversible
      mb.DropTable("MiscNumberCounters");
      mb.DropColumn("MiscNumber", "CopyRequests");
      mb.DropColumn("ReferenceNumber", "CopyRequests");
  }
  ```
  Note `nvarchar` for Arabic text and the `(ScopeKey, Year)` primary key that encodes the
  "counter is keyed by year" rule.

### When migrations actually run
This is the safety-critical part for a legal, audited system:
- **Development** — `Program.cs` calls `db.Database.MigrateAsync()` on startup (plus demo seeding) for
  convenience.
- **Production** — **nothing happens automatically.** Migrations apply **only** through the one-time,
  opt-in `ProductionBootstrap` (`Api/Bootstrap/ProductionBootstrap.cs`), gated by the
  `JCS_BOOTSTRAP=true` environment variable; you turn it off again after the first deploy. This is what
  guarantees an unintended schema change can never silently hit a live court database.

### Commands
All commands use the same project/startup flags: `-p src/ResourceIQ.Jcs.Infrastructure -s src/ResourceIQ.Jcs.Api`
(`-p` = where migrations live, `-s` = startup project that holds the connection string). Omitted below
for brevity — always include them.

- **Add** a migration: `dotnet ef migrations add <Name>`. **Read the generated file** before applying —
  make sure it does only what you intended.

**Applying.** Think of two states: the **database** (recorded in the `__EFMigrationsHistory` table) vs
the **migration files** in code.
- Dev applies pending migrations automatically on API startup (`MigrateAsync`) — usually just run the API.
- Apply manually: `dotnet ef database update` (all pending) or `dotnet ef database update <Name>` (up to a specific one).
- Production never auto-applies — only via `ProductionBootstrap` gated by `JCS_BOOTSTRAP=true`.

**Rolling back** — the command depends on whether it was applied yet:

| Situation | Command(s) |
|-----------|-----------|
| Undo — generated but **NOT applied** (DB untouched) | `dotnet ef migrations remove` (deletes the last migration's files + rewinds the snapshot) |
| Undo — **already applied** | `dotnet ef database update <PreviousName>` (runs `Down`) → then `dotnet ef migrations remove` |
| Revert **everything** (empty schema) | `dotnet ef database update 0` |

> `migrations remove` only removes the **latest** migration — revert/remove newer ones first.

**Production rollback — never run `Down` on live data.** A `Down` (`DropColumn`/`DropTable`) destroys
data and can desync numbering counters. On a live court DB, **fix forward**: write a *new* corrective
migration, review it, and deploy it through the `JCS_BOOTSTRAP` flow. Keep `Down` correct for resetting
local/dev databases only.

- Full schema reference: `DATABASE.md`.
- Remove Migration File (If Not Applied to DB Yet)
- Apply to DB.
- Rollback to Previous Migration (Keep Data) (If Applied to DB Yet)

### Worked example: add to / update a database table (end-to-end)

Remember the golden rule of Code-First (above): **you never write SQL and never edit the live database
by hand.** You change the **C# entity + its EF configuration**, then let `dotnet ef migrations add`
generate the migration, **review it**, and apply it. Below are the two everyday cases.

> All `dotnet ef` commands need the project/startup flags
> `-p src/ResourceIQ.Jcs.Infrastructure -s src/ResourceIQ.Jcs.Api` (omitted below for brevity — always
> include them). Stop the API first if it is running, or the build DLL will be locked (§17).

#### Case A — Update an existing table (add a column)

Goal: add an optional **`Notes`** field (Arabic text, up to 500 chars) to the `CopyRequests` table.

**1. Edit the entity** — `Domain/Entities/CopyRequest.cs`. Add the property with a **private setter**
(all state changes go through methods, never public setters):
```csharp
public string? Notes { get; private set; }

// expose a controlled way to change it (add near the other mutators):
public void SetNotes(string? notes) => Notes = notes?.Trim();
```

**2. Configure it in EF** — `Infrastructure/Persistence/Configurations/ModelConfigurations.cs`, inside
`CopyRequestConfiguration.Configure(...)`. Set the length so the column is `nvarchar(500)` (Unicode for
Arabic — the DB collation is `Arabic_CI_AS`):
```csharp
builder.Property(x => x.Notes).HasMaxLength(500);   // nullable by default (string?)
```

**3. Generate the migration** — EF diffs your code against the last snapshot and writes the change:
```bash
dotnet ef migrations add AddCopyRequestNotes -p src/ResourceIQ.Jcs.Infrastructure -s src/ResourceIQ.Jcs.Api
or
dotnet ef migrations add AddCopyRequestNotes --project src/ResourceIQ.Jcs.Infrastructure --startup-project src/ResourceIQ.Jcs.Api

```
This creates the usual **three files** in `Infrastructure/Persistence/Migrations/`
(`<timestamp>_AddCopyRequestNotes.cs`, its `.Designer.cs`, and the updated `JcsDbContextModelSnapshot.cs`).

**4. Review the generated `Up`/`Down`** before applying — it should do *only* what you intended:
```csharp
protected override void Up(MigrationBuilder mb) {
    mb.AddColumn<string>(
        name: "Notes", table: "CopyRequests",
        type: "nvarchar(500)", maxLength: 500, nullable: true);
}
protected override void Down(MigrationBuilder mb) {     // reversible
    mb.DropColumn(name: "Notes", table: "CopyRequests");
}
```

**5. Apply it.** In **Development** just run the API — `Program.cs` calls `MigrateAsync()` on startup.
To apply manually: `dotnet ef database update`. In **Production** it applies **only** through the
`JCS_BOOTSTRAP` flow — never automatically.

**6. Surface the new field through the stack** (a column nobody reads is useless): add it to the
relevant read DTO + query projection (`JcsQueries.cs`), mirror the type in `web/src/api/client.ts`, then
render/edit it in the UI. This is the same full path `AcceptedUtc` / `OriginalCopyId` took — grep either
name to see every file one field touches.

#### Case B — Add a brand-new table (new entity)

Goal: a new `DecisionTag` table (a lookup a decision can be labelled with).

**1. Create the entity** — `Domain/Entities/DecisionTag.cs`, private setters + a `Create` factory if it
has invariants:
```csharp
public sealed class DecisionTag {
    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    private DecisionTag() { }                                   // EF
    public static DecisionTag Create(string name) =>
        new() { Id = Guid.NewGuid(), Name = name.Trim() };
}
```

**2. Register it with EF** — add a `DbSet<DecisionTag> DecisionTags` to
`Infrastructure/Persistence/JcsDbContext.cs`, and an `IEntityTypeConfiguration<DecisionTag>` in
`ModelConfigurations.cs` (key, lengths, indexes — e.g. a unique index on `Name` if names must be
unique, BR-14 style):
```csharp
public void Configure(EntityTypeBuilder<DecisionTag> b) {
    b.HasKey(x => x.Id);
    b.Property(x => x.Name).HasMaxLength(100).IsRequired();
    b.HasIndex(x => x.Name).IsUnique();
}
```

**3. Generate + review + apply** exactly as in Case A:
```bash
dotnet ef migrations add AddDecisionTagTable -p src/ResourceIQ.Jcs.Infrastructure -s src/ResourceIQ.Jcs.Api
```
The generated `Up` will be a `mb.CreateTable("DecisionTags", …)` with a matching `mb.CreateIndex(...)`;
`Down` will be `mb.DropTable("DecisionTags")`. Review, then run the API (dev) to apply.

> **If you get the migration wrong** and have **not** applied it yet: `dotnet ef migrations remove`
> deletes the generated files and rewinds the snapshot — fix the entity/config and re-add. If you
> **already** applied it locally, roll the DB back first (`dotnet ef database update <PreviousName>`)
> then remove. **Never** run a `Down` on a live court database — fix forward (see "Production rollback"
> above).

## 16. Running & debugging quick reference
| Task | Command |
|------|---------|
| Build backend | `dotnet build` |
| Run backend (dev) | `dotnet run --project src/ResourceIQ.Jcs.Api` → `http://localhost:5253` |
| Run backend tests | `dotnet test tests/ResourceIQ.Jcs.Tests/ResourceIQ.Jcs.Tests.csproj` |
| Run frontend (dev) | `cd web && npm run dev` → `http://localhost:5173` |
| Type-check / build frontend | `cd web && npm run build` |
| Run full Docker stack | `docker compose up -d --build` (see `DEPLOY.md`) |
| Tail API logs (Docker) | `docker compose logs -f api` |

Env/ports: dev API `5253`, dev web `5173` (proxies `/api`→`5253`), LocalDB database `Jcs`. Docker:
web `:80`, SQL Server `:1433`, API internal `:8080`. Secrets live in `.env` / `appsettings.Development.json`
/ `web/.env`, which are **git-ignored — never commit them**.

## 17. Common pitfalls (things that will trip you up)
- **"DLL is locked" on `dotnet build`** → the API is still running. Stop it first.
- **Arabic shows as `?????` in a PowerShell/terminal dump** → that's just the console encoding; the
  data is correct UTF-8. Verify via the UI or a UTF-8-aware client, not the raw console.
- **A new `IJcsQueries`/repository method breaks the build** → you forgot to update `Fakes.cs`.
- **Frontend can't see a new field** → you added it server-side but didn't mirror the type in
  `client.ts`.
- **Layout looks mirrored/wrong** → you used physical CSS (`margin-left`) instead of logical
  (`margin-inline-start`); the app is RTL.
- **Back button / refresh "loses" the page** → expected; the router is in-memory (no URLs).
- **Enum mismatch** → C# enums serialize as their **names**; the TS union must use the same strings.

## 18. Domain glossary (Arabic ↔ code)
| Arabic | Code / English | Meaning |
|--------|----------------|---------|
| رئيس الديوان | `RegistryHead` | Creates copy requests; can delete the last decision; can escalate to مستعجل. |
| الناسخ | `Copyist` | Accepts then prepares the copy content. |
| المدقق | `Reviewer` | Approves / corrects / returns a copy. |
| المسؤول | `Administrator` | Manages courts/rooms/users/judges/forms; unlocks approved copies. |
| النسخة / القرار | `CopyRequest` | The core record (a judicial decision copy). |
| رقم النسخة | `CopyNumber` | Sequential, per court+year (عادي only). |
| رقم المتفرق | `MiscNumber` | Sequential per numbering scope (متفرق copies). |
| رقم الأساس | `CaseBaseNumber` | The case base number (unique per court for عادي). |
| متفرق / عادي | `Miscellaneous` / `Normal` | Decision category. متفرق links to an Approved عادي original. |
| موقوف / مستعجل / عادي | `Suspended` / `Expedited` / `Normal` | Urgency tiers (work-queue priority). |
| إعلام الحكم | the judgment PDF | Server-rendered printable output (FR-15). |

---

### Final advice for your first week
1. Run both apps locally and click through the whole workflow as each role.
2. Read `WORKFLOW.md` with `Domain/Entities/CopyRequest.cs` open side by side.
3. Pick a tiny issue, follow the recipes here, and submit a small focused change.
4. When in doubt about a business rule, check `PRD.md`/`Asd.md` — and ask. You are working on a
   legal system; ask twice, change once.

Welcome aboard. 🟢
