import { NavLink, useNavigate } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
// Style chung của mọi khu vực có sidebar (Admin / Organizer / Staff).
// theme-dark.css cung cấp .tb-btn, .tb-loading, .tb-empty và các biến --tb-*.
import "../styles/theme-dark.css";
import "../pages/admin/AdminDashboardPage.css";
import "../styles/panelReadability.css";
import "../styles/adminShell.css";

// Icon dạng SVG inline (không cần thêm thư viện).
export function Icon({ children, size = 18 }) {
  return (
    <svg
      viewBox="0 0 24 24"
      width={size}
      height={size}
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      {children}
    </svg>
  );
}

const LogoutIcon = (
  <Icon size={16}>
    <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4" />
    <polyline points="16 17 21 12 16 7" />
    <line x1="21" y1="12" x2="9" y2="12" />
  </Icon>
);

// Khung trang có sidebar dùng chung cho Admin, Organizer và Staff.
// links: [[đường dẫn, nhãn, end?], ...] hoặc [{ to, label, end, icon, active, badge }, ...]
// subnav: [[đường dẫn, nhãn], ...] — thanh tab của nhóm hiện tại (tuỳ chọn).
export default function PanelShell({
  children,
  title,
  links,
  roleLabel,
  logoutPath = "/login",
  navLabel = "Sidebar navigation",
  badges = {},
  subnav = null,
  subnavActivePath = null,
  subnavLabel = "Section navigation",
  subnavIcon = null,
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
          {links.map((link) => {
            const item = Array.isArray(link)
              ? { to: link[0], label: link[1], end: link[2], badge: badges[link[0]] }
              : link;
            const { to, label, end, icon, active, badge } = item;
            return (
              <NavLink
                key={to}
                to={to}
                end={end}
                className={({ isActive }) => {
                  const on = active === undefined ? isActive : active;
                  return `admin-nav-item${on ? " active" : ""}${badge > 0 ? " has-badge" : ""}`;
                }}
              >
                {icon && <span className="admin-nav-icon">{icon}</span>}
                <span className="admin-nav-label">{label}</span>
                {badge > 0 && (
                  <span className="admin-nav-badge" aria-label={`${badge} waiting`}>
                    {badge > 99 ? "99+" : badge}
                  </span>
                )}
              </NavLink>
            );
          })}
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
              className="admin-button admin-logout"
              onClick={() => {
                logout();
                navigate(logoutPath);
              }}
            >
              {LogoutIcon}
              Logout
            </button>
          </div>
        </header>
        {subnav && subnav.length > 0 && (
          <>
            <div className="admin-section-head">
              {subnavIcon && <span className="admin-section-icon">{subnavIcon}</span>}
              <div className="admin-section-text">
                <span className="admin-section-eyebrow">Admin</span>
                <span className="admin-section-title">{subnavLabel}</span>
              </div>
            </div>
            {subnav.length > 1 && (
              <nav className="admin-tabs" aria-label={subnavLabel}>
                {subnav.map(([to, label]) => (
                  <NavLink
                    key={to}
                    to={to}
                    className={() => `admin-tab${subnavActivePath === to ? " active" : ""}`}
                  >
                    {label}
                    {badges[to] > 0 && (
                      <span className="admin-nav-badge">{badges[to] > 99 ? "99+" : badges[to]}</span>
                    )}
                  </NavLink>
                ))}
              </nav>
            )}
          </>
        )}
        {children}
      </main>
    </div>
  );
}
