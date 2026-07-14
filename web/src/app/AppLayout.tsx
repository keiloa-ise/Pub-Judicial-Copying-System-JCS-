import { useAuth } from "../auth/AuthContext";
import { useI18n } from "../i18n";
import { useNav } from "./nav";
import { useL, roleLabels } from "./ui";
import { Emblem } from "../components/Emblem";
import type { Role } from "../api/client";

import { RequestsListPage } from "../features/requests/RequestsListPage";
import { RequestDetailPage } from "../features/requests/RequestDetailPage";
import { CreateRequestPage } from "../features/requests/CreateRequestPage";
import { PreparePage } from "../features/requests/PreparePage";
import { PrintCopyPage } from "../features/print/PrintCopyPage";
import { CourtsPage } from "../features/admin/CourtsPage";
import { NumberingLevelsPage } from "../features/admin/NumberingLevelsPage";
import { NumberingStartsPage } from "../features/admin/NumberingStartsPage";
import { DeletionOperationsPage } from "../features/requests/DeletionOperationsPage";
import { UsersPage } from "../features/admin/UsersPage";
import { JudgesPage } from "../features/admin/JudgesPage";
import { PanelTitlesPage } from "../features/admin/PanelTitlesPage";
import { ParagraphsPage } from "../features/admin/ParagraphsPage";
import { FormsPage } from "../features/admin/FormsPage";
import { ReportsDashboardPage } from "../features/reports/ReportsDashboardPage";
import { BatchPrintPage } from "../features/admin/BatchPrintPage";

interface NavItem { page: string; ar: string; en: string; }

const navByRole: Record<Role, NavItem[]> = {
  RegistryHead: [
    { page: "requests", ar: "طلباتي", en: "My requests" },
    { page: "create", ar: "طلب جديد", en: "New request" },
    { page: "deletions", ar: "عمليات الحذف", en: "Deletions" },
    { page: "reports", ar: "تقاريري", en: "My reports" },
  ],
  Copyist: [
    { page: "requests", ar: "قائمة عملي", en: "My queue" },
    { page: "reports", ar: "تقاريري", en: "My reports" },
  ],
  Reviewer: [
    { page: "requests", ar: "قائمة المراجعة", en: "Review queue" },
    { page: "reports", ar: "تقاريري", en: "My reports" },
  ],
  Administrator: [
    { page: "requests", ar: "الطلبات", en: "Requests" },
    { page: "reports", ar: "التقارير", en: "Reports" },
    { page: "admin-courts", ar: "المحاكم", en: "Courts" },
    { page: "admin-numbering", ar: "مستويات الترقيم", en: "Numbering" },
    { page: "admin-numbering-starts", ar: "بدايات الترقيم", en: "Numbering starts" },
    { page: "admin-users", ar: "المستخدمون", en: "Users" },
    { page: "admin-judges", ar: "القضاة", en: "Judges" },
    { page: "admin-panel-titles", ar: "صفات الهيئة", en: "Panel titles" },
    { page: "admin-paragraphs", ar: "الفقرات", en: "Paragraphs" },
    { page: "admin-forms", ar: "النماذج", en: "Forms" },
    { page: "admin-batch-print", ar: "طباعة دفعة", en: "Batch print" },
  ],
};

function Outlet() {
  const { route } = useNav();
  switch (route.page) {
    case "requests": return <RequestsListPage />;
    case "reports": return <ReportsDashboardPage />;
    case "request": return <RequestDetailPage id={route.id!} />;
    case "create": return <CreateRequestPage />;
    case "deletions": return <DeletionOperationsPage />;
    case "prepare": return <PreparePage id={route.id!} />;
    case "print": return <PrintCopyPage id={route.id!} />;
    case "admin-courts": return <CourtsPage />;
    case "admin-numbering": return <NumberingLevelsPage />;
    case "admin-numbering-starts": return <NumberingStartsPage />;
    case "admin-users": return <UsersPage />;
    case "admin-judges": return <JudgesPage />;
    case "admin-panel-titles": return <PanelTitlesPage />;
    case "admin-paragraphs": return <ParagraphsPage />;
    case "admin-forms": return <FormsPage />;
    case "admin-batch-print": return <BatchPrintPage />;
    default: return <RequestsListPage />;
  }
}

export function AppLayout() {
  const { user, logout } = useAuth();
  const { lang, toggle } = useI18n();
  const { route, navigate } = useNav();
  const L = useL();
  if (!user) return null;

  const items = navByRole[user.role] ?? [];
  const roleName = roleLabels[user.role]?.[lang === "ar" ? "ar" : "en"] ?? user.role;

  return (
    <div className="app">
      <header className="appbar">
        <div className="appbar__brand" onClick={() => navigate("requests")}>
          <Emblem size={40} />
          <span>{L("نظام ديوان النسخ القضائي", "Judicial Copying System")}</span>
        </div>
        <div className="appbar__spacer" />
        <button className="linkbtn" onClick={toggle}>{L("English", "العربية")}</button>
        <div className="appbar__user">
          <strong>{user.displayName}</strong>
          <span className="muted">{roleName}</span>
        </div>
        <button className="authbtn" onClick={logout}>{L("خروج", "Logout")}</button>
      </header>

      <nav className="appnav">
        {items.map((it) => (
          <button
            key={it.page}
            className={route.page === it.page ? "active" : undefined}
            onClick={() => navigate(it.page)}
          >
            {L(it.ar, it.en)}
          </button>
        ))}
      </nav>

      <main className="appmain">
        <Outlet />
      </main>
    </div>
  );
}
