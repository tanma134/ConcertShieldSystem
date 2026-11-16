import { useLocation } from "react-router-dom";
import PanelShell, { Icon } from "../../components/PanelShell";
import useAdminPending from "../../hooks/useAdminPending";


// Icon cho từng nhóm (SVG inline, cùng bộ nét 1.8).
const ICONS = {
  dashboard: (
    <Icon>
      <rect x="3" y="3" width="7" height="7" rx="1.5" />
      <rect x="14" y="3" width="7" height="7" rx="1.5" />
      <rect x="14" y="14" width="7" height="7" rx="1.5" />
      <rect x="3" y="14" width="7" height="7" rx="1.5" />
    </Icon>
  ),
  users: (
    <Icon>
      <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" />
      <circle cx="9" cy="7" r="4" />
      <path d="M23 21v-2a4 4 0 0 0-3-3.87" />
      <path d="M16 3.13a4 4 0 0 1 0 7.75" />
    </Icon>
  ),
  events: (
    <Icon>
      <rect x="3" y="4" width="18" height="18" rx="2" />
      <line x1="16" y1="2" x2="16" y2="6" />
      <line x1="8" y1="2" x2="8" y2="6" />
      <line x1="3" y1="10" x2="21" y2="10" />
    </Icon>
  ),
  organizers: (
    <Icon>
      <rect x="2" y="7" width="20" height="14" rx="2" />
      <path d="M16 21V5a2 2 0 0 0-2-2h-4a2 2 0 0 0-2 2v16" />
    </Icon>
  ),
  tickets: (
    <Icon>
      <path d="M2 9a3 3 0 0 1 0 6v2a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-2a3 3 0 0 1 0-6V7a2 2 0 0 0-2-2H4a2 2 0 0 0-2 2Z" />
      <path d="M13 5v2M13 11v2M13 17v2" />
    </Icon>
  ),
  finance: (
    <Icon>
      <rect x="1" y="4" width="22" height="16" rx="2" />
      <line x1="1" y1="10" x2="23" y2="10" />
    </Icon>
  ),
  ekyc: (
    <Icon>
      <rect x="2" y="5" width="20" height="14" rx="2" />
      <circle cx="8.5" cy="11" r="2" />
      <path d="M5.5 16c.6-1.5 1.8-2 3-2s2.4.5 3 2" />
      <line x1="14" y1="10" x2="18" y2="10" />
      <line x1="14" y1="14" x2="18" y2="14" />
    </Icon>
  ),
  risk: (
    <Icon>
      <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
      <line x1="12" y1="8" x2="12" y2="12" />
      <line x1="12" y1="16" x2="12.01" y2="16" />
    </Icon>
  ),
  queue: (
    <Icon>
      <line x1="8" y1="6" x2="21" y2="6" />
      <line x1="8" y1="12" x2="21" y2="12" />
      <line x1="8" y1="18" x2="21" y2="18" />
      <line x1="3" y1="6" x2="3.01" y2="6" />
      <line x1="3" y1="12" x2="3.01" y2="12" />
      <line x1="3" y1="18" x2="3.01" y2="18" />
    </Icon>
  ),
  support: (
    <Icon>
      <circle cx="12" cy="12" r="10" />
      <circle cx="12" cy="12" r="4" />
      <line x1="4.93" y1="4.93" x2="9.17" y2="9.17" />
      <line x1="14.83" y1="14.83" x2="19.07" y2="19.07" />
      <line x1="14.83" y1="9.17" x2="19.07" y2="4.93" />
      <line x1="4.93" y1="19.07" x2="9.17" y2="14.83" />
    </Icon>
  ),
  content: (
    <Icon>
      <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z" />
    </Icon>
  ),
  system: (
    <Icon>
      <line x1="4" y1="21" x2="4" y2="14" />
      <line x1="4" y1="10" x2="4" y2="3" />
      <line x1="12" y1="21" x2="12" y2="12" />
      <line x1="12" y1="8" x2="12" y2="3" />
      <line x1="20" y1="21" x2="20" y2="16" />
      <line x1="20" y1="12" x2="20" y2="3" />
      <line x1="1" y1="14" x2="7" y2="14" />
      <line x1="9" y1="8" x2="15" y2="8" />
      <line x1="17" y1="16" x2="23" y2="16" />
    </Icon>
  ),
};

// Mỗi nhóm = 1 mục "… Management" trên sidebar. Các trang con hiện thành tab ở đầu trang.
// Thêm trang mới: chỉ cần thêm [đường dẫn, nhãn] vào đúng nhóm (hoặc tạo nhóm mới).
export const ADMIN_GROUPS = [
  {
    key: "users",
    label: "User Management",
    items: [
      ["/admin/users", "Users"],
      ["/admin/roles", "Roles"],
    ],
  },
  {
    key: "events",
    label: "Event Management",
    items: [
      ["/admin/events", "Approvals"],
      ["/admin/events/all", "All Events"],
      ["/admin/change-requests", "Change Requests"],
      ["/admin/seating-templates", "Seating Templates"],
    ],
  },
  {
    key: "organizers",
    label: "Organizer Management",
    items: [
      ["/admin/organizer-requests", "Requests"],
      ["/admin/organizer-verifications", "Verifications"],
    ],
  },
  {
    key: "tickets",
    label: "Ticket Management",
    items: [
      ["/admin/tickets", "Tickets"],
      ["/admin/ticket-governance", "Governance"],
      ["/admin/ticket-status-history", "Status History"],
      ["/admin/returns", "Returns / Refunds"],
    ],
  },
  {
    key: "finance",
    label: "Finance Management",
    items: [
      ["/admin/vouchers", "Vouchers"],
      ["/admin/refund-reconciliation", "Refund Reconciliation"],
      ["/admin/payment-reconciliation", "Payment Exceptions"],
      ["/admin/failed-transaction-queue", "Failed Transactions"],
    ],
  },
  {
    key: "ekyc",
    label: "eKYC Management",
    items: [
      ["/admin/kyc/consent", "Consent"],
      ["/admin/kyc/deletion-requests", "Deletion Requests"],
      ["/admin/kyc-images", "Images"],
      ["/admin/kyc/settings", "Data Retention"],
      ["/admin/kyc/access-logs", "Access Logs"],
    ],
  },
  {
    key: "risk",
    label: "Fraud & Risk Management",
    items: [
      ["/admin/fraud-alerts", "Fraud Alerts"],
      ["/admin/fraud-cases", "Fraud Cases"],
      ["/admin/risk/blocks", "Risk Blocks"],
      ["/admin/risk/appeals", "Appeals"],
      ["/admin/risk/policy", "Risk Policy"],
    ],
  },
  {
    key: "queue",
    label: "Queue Management",
    items: [
      ["/admin/queue", "Virtual Queue", true],
      ["/admin/queue/abuse-alerts", "Abuse Alerts"],
    ],
  },
  {
    key: "support",
    label: "Support Management",
    items: [
      ["/admin/incidents", "Incidents"],
      ["/admin/support-cases", "Support Cases"],
    ],
  },
  {
    key: "content",
    label: "Content Management",
    items: [
      ["/admin/reviews", "Reviews"],
      ["/admin/wishlists", "Wishlists"],
    ],
  },
  {
    key: "system",
    label: "System Management",
    items: [
      ["/admin/policies", "System Policies"],
      ["/admin/audit-logs", "Activity Log"],
    ],
  },
];

// Tương thích ngược: ai import ADMIN_LINKS vẫn nhận danh sách phẳng cũ.
export const ADMIN_LINKS = [
  ["/admin", "Dashboard", true],
  ...ADMIN_GROUPS.flatMap((g) => g.items),
];

const matches = (pathname, path) => pathname === path || pathname.startsWith(`${path}/`);

// Tìm tab khớp nhất (đường dẫn dài nhất) để /admin/events/all không bị tính là /admin/events.
function findGroup(pathname) {
  let best = null;
  for (const group of ADMIN_GROUPS) {
    for (const [path] of group.items) {
      if (matches(pathname, path) && (!best || path.length > best.path.length)) {
        best = { group, path };
      }
    }
  }
  return best;
}

export default function AdminShell({ children, title = "Admin Panel", activeSubnavPath }) {
  const { pathname } = useLocation();
  // Badge đỏ để admin biết còn việc chờ duyệt.
  const pending = useAdminPending();
  const tabBadges = {
    "/admin/events": pending.events,
    "/admin/change-requests": pending.changes,
    "/admin/returns": pending.returns,
  };

  const found = findGroup(pathname);
  const activeGroup = found?.group;

  const sum = (group) =>
    group.items.reduce((total, [path]) => total + (tabBadges[path] > 0 ? tabBadges[path] : 0), 0);

  const links = [
    { to: "/admin", label: "Dashboard", end: true, icon: ICONS.dashboard },
    ...ADMIN_GROUPS.map((group) => ({
      to: group.items[0][0],
      label: group.label,
      icon: ICONS[group.key],
      active: activeGroup?.key === group.key,
      badge: sum(group),
    })),
  ];

  return (
    <PanelShell
      title={title}
      links={links}
      subnav={activeGroup ? activeGroup.items : null}
      subnavActivePath={activeSubnavPath || found?.path}
      subnavLabel={activeGroup?.label}
      subnavIcon={activeGroup ? ICONS[activeGroup.key] : null}
      roleLabel="Admin"
      logoutPath="/admin/login"
      navLabel="Admin navigation"
      badges={tabBadges}
    >
      {children}
    </PanelShell>
  );
}
