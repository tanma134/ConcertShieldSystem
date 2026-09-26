import { NavLink, useNavigate } from "react-router-dom";
import { useAuth } from "../../context/AuthContext";
// theme-dark.css định nghĩa .tb-btn / .tb-loading / .tb-empty / --tb-* dùng
// chung toàn site — phải load trước AdminDashboardPage.css để trang admin
// nào cũng có nút bấm, biến màu đầy đủ (trước đây file này không được
// import ở khu admin nên nút "tb-btn" hiển thị trần trụi, mất style).
import "../../styles/theme-dark.css";
import "./AdminDashboardPage.css";

const links = [
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
];

export default function AdminShell({ children, title = "Admin Panel" }) {
  const navigate = useNavigate();
  const { user, logout } = useAuth();

  return (
    <div className="admin-shell">
      <aside className="admin-sidebar">
        <div className="admin-brand">
          <div className="admin-brand-mark">CS</div>
          <h2>ConcertShield</h2>
        </div>
        <nav className="admin-nav" aria-label="Admin navigation">
          {links.map(([to, label, end]) => (
            <NavLink
              key={to}
              to={to}
              end={end}
              className={({ isActive }) => `admin-nav-item${isActive ? " active" : ""}`}
            >
              {label}
            </NavLink>
          ))}
        </nav>
        <div className="admin-user-box">
          <span>Logged in as</span>
          <strong>{user?.fullName || user?.email || "Admin"}</strong>
        </div>
      </aside>
      <main className="admin-main">
        <header className="admin-topbar">
          <div className="topbar-title">{title}</div>
          <div style={{ display: "flex", alignItems: "center", gap: "16px" }}>
            <button
              type="button"
              className="admin-button"
              onClick={() => {
                logout();
                navigate("/admin/login");
              }}
            >
              Logout
            </button>
          </div>
        </header>
        {children}
      </main>
    </div>
  );
}
