/**
 * Typed API client. Auth is JWT bearer; the token is obtained from
 * /api/auth/login. Authorization is always enforced server-side — this client never assumes it.
 */
const BASE = import.meta.env.VITE_API_BASE ?? "";

let token: string | null = null;
export function setToken(t: string | null) { token = t; }

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${BASE}${path}`, {
    ...init,
    headers: {
      "Content-Type": "application/json",
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(init?.headers ?? {}),
    },
  });
  if (!res.ok) {
    const body = await res.json().catch(() => ({ error: res.statusText }));
    throw new Error(body.error ?? `Request failed (${res.status})`);
  }
  return (res.status === 204 ? undefined : await res.json()) as T;
}

// ── Types (match server DTOs; enums serialize as names) ──
export type CopyState = "Created" | "InPreparation" | "UnderReview" | "Approved" | "Unlocked";
export type Role = "Administrator" | "RegistryHead" | "Copyist" | "Reviewer";
export type CaseCategory = "Normal" | "Miscellaneous";
export type CaseUrgency = "Normal" | "Suspended" | "Expedited";

export interface LoginResult { token: string; userId: string; displayName: string; role: Role; }

export interface CopyRequestListItem {
  id: string; copyNumber: string | null; state: CopyState;
  courtId: string; courtName: string; roomId: string; roomName: string;
  caseBaseNumber: string; caseFilingDate: string | null;
  reservationDate: string; category: CaseCategory; urgency: CaseUrgency;
  expediteRequestNumber: string | null; miscNumber: number | null;
  assignedCopyistId: string | null; assignedCopyistName: string | null;
  createdUtc: string; acceptedUtc: string | null;
}
export interface LinkedMisc { id: string; miscNumber: number | null; referenceNumber: string | null; state: CopyState; reservationDate: string; }
export interface CopyRequestDetail extends CopyRequestListItem {
  referenceNumber: string | null;
  formTemplateId: string | null; fieldValuesJson: string; sectionsJson: string; body: string; approvedUtc: string | null;
  originalCopyId: string | null; originalCopyNumber: string | null; linkedMisc: LinkedMisc[];
}
// CopyRequestDetail inherits acceptedUtc from CopyRequestListItem.
/** BR-11: an Approved عادي copy a متفرق can be based on (the original picker). */
export interface OriginalCopyOption { id: string; copyNumber: string; courtId: string; courtName: string; caseBaseNumber: string; reservationDate: string; }
/** A dynamic, editable section of a copy (inserted from a paragraph template). */
export interface CopySection { title: string; text: string; }
export interface AuditEntry {
  actorName: string; action: string; timestampUtc: string;
  reason: string | null; beforeJson: string | null; afterJson: string | null;
}
export interface Court { id: string; code: string; name: string; isActive: boolean; }
export type NumberingPolicy = "Court" | "Room" | "Special";
export interface Room {
  id: string; courtId: string; code: string; name: string; isActive: boolean;
  numberingPolicy: NumberingPolicy; numberingLevel: string | null;
}
/** FR-17: numbering start-point counters (admin go-live setup). */
export interface CopyNumberCounter { courtId: string; courtCode: string; courtName: string; year: number; lastNumber: number; }
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
  id: string; username: string; displayName: string; role: Role; isActive: boolean; courtIds: string[];
}
export interface Judge { id: string; name: string; isActive: boolean; roomIds: string[]; }
/** An admin-defined panel-member title (صفة), e.g. رئيس الهيئة / عضو / مستشار. */
export interface PanelMemberTitle { id: string; name: string; isActive: boolean; displayOrder: number; }
/** A judging-panel member as stored on a copy: the judge's name + the chosen title (verbatim). */
export interface PanelMember { judge: string; title: string; }
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
export type ReportExportType = "by-court" | "by-room" | "by-copyist" | "by-reviewer" | "by-head" | "by-judge" | "turnaround" | "copies";

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
  listRequests: (search?: RequestSearch) => {
    const p = new URLSearchParams();
    if (search?.state) p.set("state", search.state);
    if (search?.copyNumber) p.set("copyNumber", search.copyNumber);
    if (search?.caseBaseNumber) p.set("caseBaseNumber", search.caseBaseNumber);
    if (search?.courtId) p.set("courtId", search.courtId);
    if (search?.fromReservation) p.set("fromReservation", search.fromReservation);
    if (search?.toReservation) p.set("toReservation", search.toReservation);
    const qs = p.toString();
    return request<CopyRequestListItem[]>(`/api/copy-requests${qs ? `?${qs}` : ""}`);
  },
  getRequest: (id: string) => request<CopyRequestDetail>(`/api/copy-requests/${id}`),
  getAudit: (id: string) => request<AuditEntry[]>(`/api/copy-requests/${id}/audit`),
  // FR-15: direct same-origin URL of the server-rendered judgment PDF. Loaded straight into an
  // <iframe> (browser's native PDF viewer) — far more reliable than blob URLs. Authorized by the
  // HttpOnly "jcs_pdf" cookie set at login (the iframe can't send an Authorization header).
  pdfUrl: (id: string) => `${BASE}/api/copy-requests/${id}/pdf`,
  createRequest: (body: {
    courtId: string; roomId: string; caseFilingDate: string | null; caseBaseNumber: string;
    category: CaseCategory; urgency: CaseUrgency; expediteRequestNumber: string | null;
    referenceNumber: string | null; assignedCopyistId: string; originalCopyId: string | null;
  }) => request<{ id: string; copyNumber: string; state: string }>(
    "/api/copy-requests", { method: "POST", body: JSON.stringify(body) }),
  // FR-07: copyist accepts the copy before editing. FR-06: head escalates a non-approved copy to مستعجل.
  accept: (id: string) => request<void>(`/api/copy-requests/${id}/accept`, { method: "POST" }),
  expedite: (id: string, expediteRequestNumber: string) =>
    request<void>(`/api/copy-requests/${id}/expedite`, { method: "POST", body: JSON.stringify({ expediteRequestNumber }) }),
  // BR-11: Approved عادي copies a متفرق can be based on.
  originals: () => request<OriginalCopyOption[]>("/api/copy-requests/originals"),
  // FR-16: deletion window — latest عادي per court + last متفرق per scope; delete by copy id.
  deletionTargets: () => request<DeletionTargets>("/api/copy-requests/deletion-targets"),
  deleteRequest: (id: string) => request<void>(`/api/copy-requests/${id}`, { method: "DELETE" }),
  saveDraft: (id: string, body: { formTemplateId?: string | null; fieldValuesJson: string; sectionsJson: string; body: string }) =>
    request<void>(`/api/copy-requests/${id}/content`, { method: "PUT", body: JSON.stringify(body) }),
  submit: (id: string) => request<void>(`/api/copy-requests/${id}/submit`, { method: "POST" }),
  // FR-10: Reviewer corrects the copy in place (same body shape as saveDraft); stays under review.
  correct: (id: string, body: { formTemplateId?: string | null; fieldValuesJson: string; sectionsJson: string; body: string }) =>
    request<void>(`/api/copy-requests/${id}/correct`, { method: "PUT", body: JSON.stringify(body) }),
  approve: (id: string) => request<void>(`/api/copy-requests/${id}/approve`, { method: "POST" }),
  returnForCorrection: (id: string, corrections: string) =>
    request<void>(`/api/copy-requests/${id}/return`, { method: "POST", body: JSON.stringify({ corrections }) }),
  unlock: (id: string, reason: string) =>
    request<void>(`/api/copy-requests/${id}/unlock`, { method: "POST", body: JSON.stringify({ reason }) }),

  // ── Lookups ──
  lookupCourts: () => request<Court[]>("/api/lookups/courts"),
  lookupCopyists: (courtId: string) => request<Lookup[]>(`/api/lookups/courts/${courtId}/copyists`),
  lookupRooms: (courtId: string) => request<Room[]>(`/api/lookups/courts/${courtId}/rooms`),
  lookupJudges: (roomId: string) => request<Lookup[]>(`/api/lookups/rooms/${roomId}/judges`),
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
    turnaround: (f: ReportFilter) => request<TurnaroundReport>(`/api/reports/turnaround?${reportParams(f)}`),
    copies: (f: ReportFilter, page: number, pageSize: number) => {
      const p = reportParams(f); p.set("page", String(page)); p.set("pageSize", String(pageSize));
      return request<Paged<CopyRow>>(`/api/reports/copies?${p}`);
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
    createRoom: (courtId: string, code: string, name: string, numberingPolicy: NumberingPolicy, numberingLevel: string | null) =>
      request<{ id: string }>("/api/admin/rooms", { method: "POST", body: JSON.stringify({ courtId, code, name, numberingPolicy, numberingLevel }) }),
    updateRoom: (id: string, name: string, isActive: boolean, numberingPolicy: NumberingPolicy, numberingLevel: string | null) =>
      request<void>(`/api/admin/rooms/${id}`, { method: "PUT", body: JSON.stringify({ name, isActive, numberingPolicy, numberingLevel }) }),

    // FR-17: numbering start points.
    listCopyCounters: () => request<CopyNumberCounter[]>("/api/admin/numbering/copy-counters"),
    setCopyCounter: (courtId: string, year: number, lastNumber: number) =>
      request<void>("/api/admin/numbering/copy-counters", { method: "PUT", body: JSON.stringify({ courtId, year, lastNumber }) }),
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
  if (!res.ok) {
    const body = await res.json().catch(() => ({ error: res.statusText }));
    throw new Error(body.error ?? `Export failed (${res.status})`);
  }
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
