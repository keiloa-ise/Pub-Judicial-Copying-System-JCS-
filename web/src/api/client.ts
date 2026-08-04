/**
 * Typed API client. Auth is JWT bearer; the token is obtained from
 * /api/auth/login. Authorization is always enforced server-side — this client never assumes it.
 */
// Empty base => relative /api/... calls on the SPA's own origin, forwarded by the Vite dev proxy
// (dev) or Nginx (prod). Set VITE_API_BASE only to point the SPA at a different-origin API.
const BASE = import.meta.env.VITE_API_BASE ?? "";

let token: string | null = null;
export function setToken(t: string | null) { token = t; }

export class ApiError extends Error {
  status: number;
  messages: string[];

  constructor(status: number, messages: string[]) {
    super(messages.join("\n"));
    this.name = "ApiError";
    this.status = status;
    this.messages = messages;
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}

function pushMessage(target: string[], value: unknown) {
  if (typeof value !== "string") return;
  const trimmed = value.trim();
  if (trimmed && !target.includes(trimmed)) target.push(trimmed);
}

function collectValidationMessages(errors: unknown): string[] {
  const messages: string[] = [];
  if (Array.isArray(errors)) {
    errors.forEach((x) => pushMessage(messages, x));
    return messages;
  }
  if (!isRecord(errors)) return messages;

  for (const value of Object.values(errors)) {
    if (Array.isArray(value)) value.forEach((x) => pushMessage(messages, x));
    else pushMessage(messages, value);
  }
  return messages;
}

function errorMessages(body: unknown, fallback: string): string[] {
  const messages: string[] = [];
  if (isRecord(body)) {
    const validationMessages = collectValidationMessages(body.errors);
    validationMessages.forEach((x) => pushMessage(messages, x));
    if (validationMessages.length === 0) {
      pushMessage(messages, body.error);
      pushMessage(messages, body.detail);
      pushMessage(messages, body.title);
    }
  } else {
    pushMessage(messages, body);
  }
  if (messages.length === 0) pushMessage(messages, fallback);
  return messages;
}

async function readErrorBody(res: Response): Promise<unknown> {
  const text = await res.text().catch(() => "");
  if (!text) return null;
  try { return JSON.parse(text); } catch { return text; }
}

async function throwApiError(res: Response, fallback: string): Promise<never> {
  const body = await readErrorBody(res);
  throw new ApiError(res.status, errorMessages(body, fallback));
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${BASE}${path}`, {
    ...init,
    headers: {
      "Content-Type": "application/json",
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(init?.headers ?? {}),
    },
  });
  if (!res.ok) await throwApiError(res, `Request failed (${res.status})`);
  return (res.status === 204 ? undefined : await res.json()) as T;
}

// ── Types (match server DTOs; enums serialize as names) ──
export type CopyState = "Created" | "InPreparation" | "UnderReview" | "Approved" | "Unlocked";
export type Role = "Administrator" | "RegistryHead" | "Copyist" | "Reviewer";
export type CaseCategory = "Normal" | "Miscellaneous";
export type CaseUrgency = "Normal" | "Suspended" | "Expedited";

export interface LoginResult { token: string; userId: string; displayName: string; role: Role; }
/** FR-15 feature flags (both default true). Server enforces; the SPA uses these only to hide options. */
export interface FeatureFlags { allowCopyistReprint: boolean; allowHeadBatchPrint: boolean; allowDeleteApproved: boolean; }
/** JC-32: a recoverable form draft returned by the server. */
export interface FormDraft<TPayload = unknown> {
  formKey: string; role: string; copyRequestId: string | null; payload: TPayload; updatedAt: string;
}

export interface CopyRequestListItem {
  id: string; copyNumber: string | null; state: CopyState;
  courtId: string; courtName: string; roomId: string; roomName: string;
  caseBaseNumber: string; caseFilingDate: string | null;
  reservationDate: string; category: CaseCategory; urgency: CaseUrgency;
  expediteRequestNumber: string | null; miscNumber: number | null;
  assignedCopyistId: string | null; assignedCopyistName: string | null;
  createdUtc: string; acceptedUtc: string | null;
  /** Set when the copy was returned to the copyist for correction and is still awaiting it. */
  returnedForCorrectionUtc?: string | null;
}
export interface LinkedMisc { id: string; miscNumber: number | null; referenceNumber: string | null; state: CopyState; reservationDate: string; }
export interface CopyRequestDetail extends CopyRequestListItem {
  referenceNumber: string | null;
  suspendRequestNumber: string | null; // FR-06: optional note captured on escalation to موقوف.
  formTemplateId: string | null; fieldValuesJson: string; sectionsJson: string; dissentSectionsJson: string; rebuttalSectionsJson: string; body: string; approvedUtc: string | null;
  originalCopyId: string | null; originalCopyNumber: string | null; linkedMisc: LinkedMisc[];
  printedUtc: string | null; // FR-15: set when printed in the current phase; blocks re-print of approved copies.
}
// CopyRequestDetail inherits acceptedUtc from CopyRequestListItem.
/** BR-11: an Approved عادي copy a متفرق can be based on (the original picker). Carries room so the
 *  create form can narrow the picker to the chosen court+room. */
export interface OriginalCopyOption { id: string; copyNumber: string; courtId: string; courtName: string; roomId: string; roomName: string; caseBaseNumber: string; reservationDate: string; }
/** FR-03/FR-06: last sequential number issued for a court/room scope this year, and the next to allocate. */
export interface LastNumber { last: number | null; next: number; }
/** A dynamic, editable section of a copy (inserted from a paragraph template). */
export interface CopySection { title: string; text: string; }
export interface AuditEntry {
  actorName: string; action: string; timestampUtc: string;
  reason: string | null; beforeJson: string | null; afterJson: string | null;
}
export interface Court { id: string; code: string; name: string; isActive: boolean; }
export type NumberingPolicy = "Court" | "Room" | "Special";
/** رقم النسخة (عادي) numbering scope for a room (FR-03). Default Room. */
export type CopyNumberingPolicy = "Court" | "Room";
export interface Room {
  id: string; courtId: string; code: string; name: string; isActive: boolean;
  numberingPolicy: NumberingPolicy; numberingLevel: string | null;
  copyNumberingPolicy: CopyNumberingPolicy;
}
/** FR-17: numbering start-point counters (admin go-live setup). */
export interface CopyNumberCounter { courtId: string; courtCode: string; courtName: string; roomId: string | null; scopeLabel: string; year: number; lastNumber: number; }
export interface MiscNumberCounter { scopeKey: string; courtId: string; courtName: string; scopeLabel: string; year: number; lastNumber: number; }

/** FR-16: the latest عادي copy per court — deletable only when it has no linked متفرق. */
export interface DeletableCopy {
  courtId: string; courtName: string; copyRequestId: string; copyNumber: string;
  roomName: string; state: CopyState; hasLinkedMisc: boolean;
}
/** FR-16: the last متفرق per numbering scope — deletable by its scope. */
export interface DeletableMisc {
  scopeKey: string; courtId: string; courtName: string; scopeLabel: string;
  copyRequestId: string; miscNumber: number; originalCopyNumber: string | null; referenceNumber: string | null; state: CopyState;
}
export interface DeletionTargets { normals: DeletableCopy[]; miscs: DeletableMisc[]; }
export interface Lookup { id: string; name: string; }
export interface UserDto {
  id: string; username: string; displayName: string; role: Role; isActive: boolean;
  courtIds: string[]; roomIds: string[];
}
/** A user assigned to a court/room, for the admin per-court/per-room assignee panels. */
export interface AssignedUser { id: string; username: string; displayName: string; role: Role; }
export interface Judge { id: string; name: string; isActive: boolean; roomIds: string[]; }
/** An admin-defined panel-member title (صفة), e.g. رئيس الهيئة / عضو / مستشار. */
export interface PanelMemberTitle { id: string; name: string; isActive: boolean; displayOrder: number; }
/** A judging-panel member as stored on a copy: the judge's name + the chosen title (verbatim). */
export interface PanelMember { judge: string; title: string; dissenting?: boolean; replying?: boolean; delegated?: boolean; delegationDate?: string; delegationNumber?: string; }
export interface ParagraphTemplate { id: string; title: string; body: string; isArchived: boolean; formTemplateId: string | null; }
export interface FormField { id: string; key: string; label: string; type: string; validationRulesJson: string | null; order: number; }
export interface FormTemplate { id: string; name: string; isActive: boolean; fields: FormField[]; }
export interface RequestSearch {
  state?: CopyState; copyNumber?: string; caseBaseNumber?: string;
  courtId?: string; fromReservation?: string; toReservation?: string;
}

// ── Reporting (FR-13) ──
export interface ReportFilter {
  fromDate?: string; toDate?: string; status?: CopyState;
  courtId?: string; roomId?: string; copyistId?: string; reviewerId?: string;
}
export interface CountRow {
  id: string | null; name: string; total: number;
  inPreparation: number; underReview: number; approved: number; unlocked: number;
}
export interface TurnaroundStat { id: string | null; name: string; count: number; avgHours: number; minHours: number; maxHours: number; }
export interface TurnaroundReport { byCourt: TurnaroundStat[]; byCopyist: TurnaroundStat[]; }
export interface CopyRow {
  id: string; copyNumber: string | null; courtName: string; roomName: string; caseBaseNumber: string;
  copyistName: string | null; reviewerName: string | null; state: CopyState;
  createdUtc: string; approvedUtc: string | null; turnaroundHours: number | null;
}
export interface ReportSummary {
  totalCopies: number; inPreparation: number; underReview: number; approved: number; unlocked: number;
  approvedWithTurnaround: number; avgTurnaroundHours: number;
  acceptedCount: number; avgAcceptanceHours: number;
}
export interface Paged<T> { items: T[]; total: number; page: number; pageSize: number; }
/** FR-13: one line of the per-judge work log — a decision the judge sat on, with role + delegation. */
export interface JudgeWorkLogRow {
  judgeName: string; copyNumber: string | null; miscNumber: number | null; decisionNumber: string | null;
  courtName: string; roomName: string; reservationDate: string; state: CopyState; role: string;
  delegated: boolean; delegationNumber: string | null; delegationDate: string | null;
}
export interface CopyistAccuracyRow {
  copyistId: string | null; copyistName: string;
  decisionsCorrected: number; returnCycles: number;
  totalWordsCorrected: number; totalWords: number; avgCorrectionRate: number; // fraction, e.g. 0.14 = 14%
}
export type ReportExportType = "by-court" | "by-room" | "by-copyist" | "by-reviewer" | "by-head" | "by-judge" | "judge-work-log" | "copyist-accuracy" | "turnaround" | "copies";

function reportParams(f: ReportFilter): URLSearchParams {
  const p = new URLSearchParams();
  if (f.fromDate) p.set("fromDate", f.fromDate);
  if (f.toDate) p.set("toDate", f.toDate);
  if (f.status) p.set("status", f.status);
  if (f.courtId) p.set("courtId", f.courtId);
  if (f.roomId) p.set("roomId", f.roomId);
  if (f.copyistId) p.set("copyistId", f.copyistId);
  if (f.reviewerId) p.set("reviewerId", f.reviewerId);
  return p;
}

export const api = {
  // ── Auth ──
  login: (username: string, password: string) =>
    request<LoginResult>("/api/auth/login", { method: "POST", body: JSON.stringify({ username, password }) }),
  logout: () => request<void>("/api/auth/logout", { method: "POST" }).catch(() => {}),

  // ── Copy requests ──
  listRequests: (search?: RequestSearch, page = 1, pageSize = 50) => {
    const p = new URLSearchParams();
    if (search?.state) p.set("state", search.state);
    if (search?.copyNumber) p.set("copyNumber", search.copyNumber);
    if (search?.caseBaseNumber) p.set("caseBaseNumber", search.caseBaseNumber);
    if (search?.courtId) p.set("courtId", search.courtId);
    if (search?.fromReservation) p.set("fromReservation", search.fromReservation);
    if (search?.toReservation) p.set("toReservation", search.toReservation);
    p.set("page", String(page)); p.set("pageSize", String(pageSize));
    return request<Paged<CopyRequestListItem>>(`/api/copy-requests?${p.toString()}`);
  },
  getRequest: (id: string) => request<CopyRequestDetail>(`/api/copy-requests/${id}`),
  getAudit: (id: string) => request<AuditEntry[]>(`/api/copy-requests/${id}/audit`),
  // FR-15 batch print (Administrator): preview the matching copies (court+room+date range+kind).
  batchPrintPreview: (courtId: string, roomId: string, from: string, to: string, approved: boolean) =>
    request<CopyRequestListItem[]>(
      `/api/copy-requests/batch-print/preview?courtId=${courtId}&roomId=${roomId}&from=${from}&to=${to}&approved=${approved}`),
  // FR-15 batch print: download a ZIP with one independent PDF per matching decision.
  batchPrintZip: async (courtId: string, roomId: string, from: string, to: string, approved: boolean): Promise<Blob> => {
    const res = await fetch(
      `${BASE}/api/copy-requests/batch-print?courtId=${courtId}&roomId=${roomId}&from=${from}&to=${to}&approved=${approved}`,
      { headers: { ...(token ? { Authorization: `Bearer ${token}` } : {}) } });
    if (!res.ok) await throwApiError(res, `Request failed (${res.status})`);
    return res.blob();
  },
  // FR-15: direct same-origin URL of the server-rendered judgment PDF. Loaded straight into an
  // <iframe> (browser's native PDF viewer) — far more reliable than blob URLs. Authorized by the
  // HttpOnly "jcs_pdf" cookie set at login (the iframe can't send an Authorization header).
  pdfUrl: (id: string) => `${BASE}/api/copy-requests/${id}/pdf`,
  // FR-15: PRINT (not preview) — enforces print order + once-per-approval, records the print, and
  // returns the PDF bytes to send to the printer. Throws (with the server's Arabic reason) if blocked.
  printPdf: async (id: string): Promise<Blob> => {
    const res = await fetch(`${BASE}/api/copy-requests/${id}/print`, {
      method: "POST", headers: { ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    });
    if (!res.ok) await throwApiError(res, `Request failed (${res.status})`);
    return res.blob();
  },
  createRequest: (body: {
    courtId: string; roomId: string; caseFilingDate: string | null; caseBaseNumber: string;
    category: CaseCategory; urgency: CaseUrgency; expediteRequestNumber: string | null;
    referenceNumber: string | null; assignedCopyistId: string; originalCopyId: string | null;
    year?: string | null; issueHijri?: string | null; issueGregorian?: string | null;
    firstBaseNumber?: string | null;
  }) => request<{ id: string; copyNumber: string; state: string }>(
    "/api/copy-requests", { method: "POST", body: JSON.stringify(body) }),
  // FR-07: copyist accepts the copy before editing. FR-06: head escalates a non-approved copy.
  accept: (id: string) => request<void>(`/api/copy-requests/${id}/accept`, { method: "POST" }),
  expedite: (id: string, expediteRequestNumber: string) =>
    request<void>(`/api/copy-requests/${id}/expedite`, { method: "POST", body: JSON.stringify({ expediteRequestNumber }) }),
  suspend: (id: string, note?: string | null) =>
    request<void>(`/api/copy-requests/${id}/suspend`, { method: "POST", body: JSON.stringify({ note: note ?? null }) }),
  // BR-11: Approved عادي copies a متفرق can be based on.
  // BR-11: Approved originals for the متفرق picker — filtered server-side to a room (+ optional search),
  // capped server-side, so the payload stays small no matter how many approved copies exist.
  originals: (roomId: string, search: string) =>
    request<OriginalCopyOption[]>(`/api/copy-requests/originals?roomId=${roomId}&search=${encodeURIComponent(search)}`),
  // FR-03/FR-06: last issued sequential number for a court/room scope (عادي → رقم النسخة, متفرق → رقم المتفرق).
  lastNumber: (courtId: string, roomId: string, category: CaseCategory, year: number) =>
    request<LastNumber>(`/api/copy-requests/last-number?courtId=${courtId}&roomId=${roomId}&category=${category}&year=${year}`),
  // FR-16: deletion window — latest عادي per court + last متفرق per scope; delete by copy id.
  deletionTargets: () => request<DeletionTargets>("/api/copy-requests/deletion-targets"),
  deleteRequest: (id: string) => request<void>(`/api/copy-requests/${id}`, { method: "DELETE" }),
  saveDraft: (id: string, body: { formTemplateId?: string | null; fieldValuesJson: string; sectionsJson: string; dissentSectionsJson: string; rebuttalSectionsJson: string; body: string }) =>
    request<void>(`/api/copy-requests/${id}/content`, { method: "PUT", body: JSON.stringify(body) }),
  submit: (id: string) => request<void>(`/api/copy-requests/${id}/submit`, { method: "POST" }),
  // FR-10: Reviewer corrects the copy in place (same body shape as saveDraft); stays under review.
  correct: (id: string, body: { formTemplateId?: string | null; fieldValuesJson: string; sectionsJson: string; dissentSectionsJson: string; rebuttalSectionsJson: string; body: string }) =>
    request<void>(`/api/copy-requests/${id}/correct`, { method: "PUT", body: JSON.stringify(body) }),
  approve: (id: string) => request<void>(`/api/copy-requests/${id}/approve`, { method: "POST" }),
  returnForCorrection: (id: string, corrections: string) =>
    request<void>(`/api/copy-requests/${id}/return`, { method: "POST", body: JSON.stringify({ corrections }) }),
  unlock: (id: string, reason: string) =>
    request<void>(`/api/copy-requests/${id}/unlock`, { method: "POST", body: JSON.stringify({ reason }) }),

  // ── Lookups ──
  lookupCourts: () => request<Court[]>("/api/lookups/courts"),
  lookupCopyists: (roomId: string) => request<Lookup[]>(`/api/lookups/rooms/${roomId}/copyists`),
  lookupRooms: (courtId: string) => request<Room[]>(`/api/lookups/courts/${courtId}/rooms`),
  lookupJudges: (roomId: string) => request<Lookup[]>(`/api/lookups/rooms/${roomId}/judges`),
  /** FR-19-adjacent: all active judges (any court/room) — for a delegated (ندباً) panel member. */
  lookupAllJudges: () => request<Lookup[]>("/api/lookups/judges"),
  lookupPanelTitles: () => request<Lookup[]>("/api/lookups/panel-titles"),
  lookupParagraphs: (formTemplateId?: string) =>
    request<ParagraphTemplate[]>(`/api/lookups/paragraph-templates${formTemplateId ? `?formTemplateId=${formTemplateId}` : ""}`),
  lookupForms: () => request<FormTemplate[]>("/api/lookups/form-templates"),

  // ── Reports (FR-13) ──
  reports: {
    summary: (f: ReportFilter) => request<ReportSummary>(`/api/reports/summary?${reportParams(f)}`),
    byCourt: (f: ReportFilter) => request<CountRow[]>(`/api/reports/by-court?${reportParams(f)}`),
    byRoom: (f: ReportFilter) => request<CountRow[]>(`/api/reports/by-room?${reportParams(f)}`),
    byCopyist: (f: ReportFilter) => request<CountRow[]>(`/api/reports/by-copyist?${reportParams(f)}`),
    byReviewer: (f: ReportFilter) => request<CountRow[]>(`/api/reports/by-reviewer?${reportParams(f)}`),
    byHead: (f: ReportFilter) => request<CountRow[]>(`/api/reports/by-head?${reportParams(f)}`),
    byJudge: (f: ReportFilter) => request<CountRow[]>(`/api/reports/by-judge?${reportParams(f)}`),
    judgeWorkLog: (f: ReportFilter, page: number, pageSize: number) => {
      const p = reportParams(f); p.set("page", String(page)); p.set("pageSize", String(pageSize));
      return request<Paged<JudgeWorkLogRow>>(`/api/reports/judge-work-log?${p}`);
    },
    copyistAccuracy: (f: ReportFilter) => request<CopyistAccuracyRow[]>(`/api/reports/copyist-accuracy?${reportParams(f)}`),
    turnaround: (f: ReportFilter) => request<TurnaroundReport>(`/api/reports/turnaround?${reportParams(f)}`),
    copies: (f: ReportFilter, page: number, pageSize: number) => {
      const p = reportParams(f); p.set("page", String(page)); p.set("pageSize", String(pageSize));
      return request<Paged<CopyRow>>(`/api/reports/copies?${p}`);
    },
  },

  // ── Feature flags (server-authoritative; used to hide role-gated UI) ──
  config: () => request<FeatureFlags>("/api/config"),

  // ── JC-32 recoverable form drafts (per user + form key) ──
  getFormDraft: <TPayload = unknown>(formKey: string) =>
    request<FormDraft<TPayload> | null>(`/api/form-drafts/${encodeURIComponent(formKey)}`),
  upsertFormDraft: <TPayload = unknown>(formKey: string, payload: TPayload, copyRequestId?: string | null) =>
    request<FormDraft<TPayload>>(`/api/form-drafts/${encodeURIComponent(formKey)}`, {
      method: "PUT", body: JSON.stringify({ payload, copyRequestId: copyRequestId ?? null }),
    }),
  deleteFormDraft: (formKey: string) =>
    request<void>(`/api/form-drafts/${encodeURIComponent(formKey)}`, { method: "DELETE" }),

  // ── FR-15 print queues ──
  printQueue: {
    reviewer: () => request<CopyRequestListItem[]>("/api/print-queue/reviewer"),
    copyist: () => request<CopyRequestListItem[]>("/api/print-queue/copyist"),
    // Marks the selected decisions printed and returns them as ONE merged PDF blob to print.
    print: async (ids: string[]): Promise<Blob> => {
      const res = await fetch(`${BASE}/api/print-queue/print`, {
        method: "POST",
        headers: { "Content-Type": "application/json", ...(token ? { Authorization: `Bearer ${token}` } : {}) },
        body: JSON.stringify({ ids }),
      });
      if (!res.ok) await throwApiError(res, `Request failed (${res.status})`);
      return res.blob();
    },
  },

  // ── Admin ──
  admin: {
    listCourts: () => request<Court[]>("/api/admin/courts"),
    createCourt: (code: string, name: string) =>
      request<{ id: string }>("/api/admin/courts", { method: "POST", body: JSON.stringify({ code, name }) }),
    updateCourt: (id: string, name: string, isActive: boolean) =>
      request<void>(`/api/admin/courts/${id}`, { method: "PUT", body: JSON.stringify({ name, isActive }) }),

    listRooms: (courtId?: string) =>
      request<Room[]>(`/api/admin/rooms${courtId ? `?courtId=${courtId}` : ""}`),
    createRoom: (courtId: string, code: string, name: string, numberingPolicy: NumberingPolicy, numberingLevel: string | null, copyNumberingPolicy: CopyNumberingPolicy) =>
      request<{ id: string }>("/api/admin/rooms", { method: "POST", body: JSON.stringify({ courtId, code, name, numberingPolicy, numberingLevel, copyNumberingPolicy }) }),
    updateRoom: (id: string, name: string, isActive: boolean, numberingPolicy: NumberingPolicy, numberingLevel: string | null, copyNumberingPolicy: CopyNumberingPolicy) =>
      request<void>(`/api/admin/rooms/${id}`, { method: "PUT", body: JSON.stringify({ name, isActive, numberingPolicy, numberingLevel, copyNumberingPolicy }) }),

    // FR-17: numbering start points.
    listCopyCounters: () => request<CopyNumberCounter[]>("/api/admin/numbering/copy-counters"),
    setCopyCounter: (courtId: string, roomId: string | null, year: number, lastNumber: number) =>
      request<void>("/api/admin/numbering/copy-counters", { method: "PUT", body: JSON.stringify({ courtId, roomId, year, lastNumber }) }),
    listMiscCounters: () => request<MiscNumberCounter[]>("/api/admin/numbering/misc-counters"),
    setMiscCounter: (courtId: string, scope: NumberingPolicy, roomId: string | null, level: string | null, year: number, lastNumber: number) =>
      request<void>("/api/admin/numbering/misc-counters", { method: "PUT", body: JSON.stringify({ courtId, scope, roomId, level, year, lastNumber }) }),

    listUsers: () => request<UserDto[]>("/api/admin/users"),
    createUser: (body: { username: string; displayName: string; role: Role; password: string; courtIds: string[] }) =>
      request<{ id: string }>("/api/admin/users", { method: "POST", body: JSON.stringify(body) }),
    updateUser: (id: string, displayName: string, role: Role) =>
      request<void>(`/api/admin/users/${id}`, { method: "PUT", body: JSON.stringify({ displayName, role }) }),
    setUserActive: (id: string, isActive: boolean) =>
      request<void>(`/api/admin/users/${id}/active`, { method: "PUT", body: JSON.stringify({ isActive }) }),
    setUserCourts: (id: string, courtIds: string[]) =>
      request<void>(`/api/admin/users/${id}/courts`, { method: "PUT", body: JSON.stringify({ courtIds }) }),
    setUserRooms: (id: string, roomIds: string[]) =>
      request<void>(`/api/admin/users/${id}/rooms`, { method: "PUT", body: JSON.stringify({ roomIds }) }),
    // Per-room / per-court assignee panels (view + unassign)
    roomUsers: (roomId: string) => request<AssignedUser[]>(`/api/admin/rooms/${roomId}/users`),
    unassignRoomUser: (roomId: string, userId: string) =>
      request<void>(`/api/admin/rooms/${roomId}/users/${userId}`, { method: "DELETE" }),
    courtUsers: (courtId: string) => request<AssignedUser[]>(`/api/admin/courts/${courtId}/users`),
    unassignCourtUser: (courtId: string, userId: string) =>
      request<void>(`/api/admin/courts/${courtId}/users/${userId}`, { method: "DELETE" }),
    resetPassword: (id: string, password: string) =>
      request<void>(`/api/admin/users/${id}/password`, { method: "PUT", body: JSON.stringify({ password }) }),

    listJudges: () => request<Judge[]>("/api/admin/judges"),
    createJudge: (name: string, roomIds: string[]) =>
      request<{ id: string }>("/api/admin/judges", { method: "POST", body: JSON.stringify({ name, roomIds }) }),
    updateJudge: (id: string, name: string, isActive: boolean, roomIds: string[]) =>
      request<void>(`/api/admin/judges/${id}`, { method: "PUT", body: JSON.stringify({ name, isActive, roomIds }) }),

    listPanelTitles: () => request<PanelMemberTitle[]>("/api/admin/panel-titles"),
    createPanelTitle: (name: string, displayOrder: number) =>
      request<{ id: string }>("/api/admin/panel-titles", { method: "POST", body: JSON.stringify({ name, displayOrder }) }),
    updatePanelTitle: (id: string, name: string, isActive: boolean, displayOrder: number) =>
      request<void>(`/api/admin/panel-titles/${id}`, { method: "PUT", body: JSON.stringify({ name, isActive, displayOrder }) }),

    listParagraphs: () => request<ParagraphTemplate[]>("/api/admin/paragraph-templates"),
    createParagraph: (title: string, body: string, formTemplateId: string | null) =>
      request<{ id: string }>("/api/admin/paragraph-templates", { method: "POST", body: JSON.stringify({ title, body, formTemplateId }) }),
    updateParagraph: (id: string, title: string, body: string, isArchived: boolean, formTemplateId: string | null) =>
      request<void>(`/api/admin/paragraph-templates/${id}`, { method: "PUT", body: JSON.stringify({ title, body, isArchived, formTemplateId }) }),

    listForms: () => request<FormTemplate[]>("/api/admin/form-templates"),
    createForm: (name: string, fields: { key: string; label: string; type: string; validationRulesJson: string | null; order: number }[]) =>
      request<{ id: string }>("/api/admin/form-templates", { method: "POST", body: JSON.stringify({ name, fields }) }),
    updateForm: (id: string, name: string, isActive: boolean, fields: { key: string; label: string; type: string; validationRulesJson: string | null; order: number }[]) =>
      request<void>(`/api/admin/form-templates/${id}`, { method: "PUT", body: JSON.stringify({ name, isActive, fields }) }),
  },
};

/**
 * Downloads a report export. A plain <a href> can't carry the JWT, so we fetch with the bearer
 * header, read the blob + the server's Content-Disposition filename, and trigger a save.
 */
export async function downloadReport(type: ReportExportType, format: "csv" | "xlsx", f: ReportFilter): Promise<void> {
  const p = reportParams(f); p.set("type", type); p.set("format", format);
  const res = await fetch(`${BASE}/api/reports/export?${p}`, {
    headers: { ...(token ? { Authorization: `Bearer ${token}` } : {}) },
  });
  if (!res.ok) await throwApiError(res, `Export failed (${res.status})`);
  const blob = await res.blob();
  const fallback = `${type}.${format}`;
  const fileName = parseFileName(res.headers.get("Content-Disposition")) ?? fallback;

  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url; a.download = fileName;
  document.body.appendChild(a); a.click();
  a.remove(); URL.revokeObjectURL(url);
}

/** Reads filename from a Content-Disposition header, preferring RFC 5987 filename* (UTF-8). */
function parseFileName(header: string | null): string | null {
  if (!header) return null;
  const star = /filename\*=UTF-8''([^;]+)/i.exec(header);
  if (star) { try { return decodeURIComponent(star[1]); } catch { /* fall through */ } }
  const plain = /filename="?([^";]+)"?/i.exec(header);
  return plain ? plain[1] : null;
}
