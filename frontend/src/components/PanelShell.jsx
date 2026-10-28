import { NavLink, useNavigate } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
// Style chung của mọi khu vực có sidebar (Admin / Organizer / Staff).
// theme-dark.css cung cấp .tb-btn, .tb-loading, .tb-empty và các biến --tb-*.
import "../styles/theme-dark.css";
import "../pages/admin/AdminDashboardPage.css";
import "../styles/panelReadability.css";

// Khung trang có sidebar dùng chung cho Admin, Organizer và Staff.
// links: [[đường dẫn, nhãn, end?], ...]; mỗi khu vực chỉ khác danh sách link,
// nên sidebar luôn có cùng bố cục, cùng class CSS và cùng hành vi.
export default function PanelShell({
  children,
  title,
  links,
  roleLabel,
  logoutPath = "/login",
  navLabel = "Sidebar navigation",
  badges = {},
}) {
  const navigate = useNavigate();
  const { user, logout } = useAuth();

  return (
    <div className="admin-shell">
      <aside className="admin-sidebar">
        <div className="admin-brand">
          <div className="admin-brand-mark">CS</div>
          <h2>ConcertShield</h2>
        </div>
        <nav className="admin-nav" aria-label={navLabel}>
          {links.map(([to, label, end]) => (
            <NavLink
              key={to}
              to={to}
              end={end}
              className={({ isActive }) =>
                `admin-nav-item${isActive ? " active" : ""}${badges[to] > 0 ? " has-badge" : ""}`
              }
            >
              {label}
              {badges[to] > 0 && (
                <span className="admin-nav-badge" aria-label={`${badges[to]} waiting`}>
                  {badges[to] > 99 ? "99+" : badges[to]}
                </span>
              )}
            </NavLink>
          ))}
        </nav>
        <div className="admin-user-box">
          <span>Logged in as</span>
          <strong>{user?.fullName || user?.email || roleLabel}</strong>
        </div>
      </aside>
      <main className="admin-main">
        <header className="admin-topbar">
          <div className="topbar-title">{title || `${roleLabel} Panel`}</div>
          <div style={{ display: "flex", alignItems: "center", gap: "16px" }}>
            <button
              type="button"
              className="admin-button"
              onClick={() => {
                logout();
                navigate(logoutPath);
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
