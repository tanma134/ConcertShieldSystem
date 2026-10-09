import PanelShell from "../../components/PanelShell";
import useAdminPending from "../../hooks/useAdminPending";

// Một danh sách duy nhất cho toàn bộ trang admin — thêm trang mới chỉ cần sửa ở đây.
export const ADMIN_LINKS = [
  ["/admin", "Dashboard", true],
  ["/admin/users", "User Management"],
  ["/admin/roles", "Role Management"],
  ["/admin/events", "Event Approvals"],
  ["/admin/events/all", "All Events"],
  ["/admin/organizer-requests", "Organizer Requests"],
  ["/admin/reviews", "Review Management"],
  ["/admin/wishlists", "Wishlist Management"],
  ["/admin/seating-templates", "Seating Templates"],
  ["/admin/vouchers", "Voucher Management"],
  ["/admin/change-requests", "Change Requests"],
  ["/admin/returns", "Ticket Returns / Refunds"],
  ["/admin/kyc/consent", "eKYC Consent"],
  ["/admin/kyc/deletion-requests", "eKYC Deletion Requests"],
  ["/admin/kyc-images", "eKYC Images"],
  ["/admin/kyc/settings", "eKYC Data Retention"],
  ["/admin/kyc/access-logs", "eKYC Access Logs"],
  ["/admin/audit-logs", "Activity Log"],
  ["/admin/fraud-alerts", "Fraud Alert Dashboard"],
  ["/admin/risk/blocks", "Risk Blocks"],
  ["/admin/risk/appeals", "Appeals"]
];

export default function AdminShell({ children, title = "Admin Panel" }) {
  // Badge đỏ trên sidebar để admin biết còn việc chờ duyệt.
  const pending = useAdminPending();
  const badges = {
    "/admin/events": pending.events,
    "/admin/change-requests": pending.changes,
    "/admin/returns": pending.returns,
  };

  return (
    <PanelShell
      title={title}
      links={ADMIN_LINKS}
      roleLabel="Admin"
      logoutPath="/admin/login"
      navLabel="Admin navigation"
      badges={badges}
    >
      {children}
    </PanelShell>
  );
}
