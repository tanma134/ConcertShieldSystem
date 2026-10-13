import { useEffect, useState, useCallback } from "react";
import AdminShell from "./AdminShell";
import { Link } from "react-router-dom";
import useAdminPending from "../../hooks/useAdminPending";
import adminApi from "../../api/adminApi";
import { useAuth } from "../../context/AuthContext";
import "./AdminDashboardPage.css";

const normalizeRoles = (user) => {
  if (!user) return [];
  if (Array.isArray(user.roles)) return user.roles;
  if (user.roleName) return [{ roleName: user.roleName }];
  return [];
};

const roleName = (role) => String(role?.roleName || role || "");

export default function AdminDashboardPage() {
  const { user } = useAuth();
  const pending = useAdminPending();

  const [totalUsers, setTotalUsers] = useState(null);
  const [activeUsers, setActiveUsers] = useState(null);
  const [totalRoles, setTotalRoles] = useState(null);
  const [adminCount, setAdminCount] = useState(null);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);

  const loadSummary = useCallback(async (signal) => {
    setLoading(true);
    setError("");
    try {
      const [usersResponse, rolesResponse] = await Promise.all([
        adminApi.getUsers(signal),
        adminApi.getRoles(signal),
      ]);

      // Nếu request bị hủy (component unmount) thì không set state nữa
      if (signal?.aborted) return;

      const users = usersResponse?.data || [];
      const roles = rolesResponse?.data || [];

      setTotalUsers(users.length);
      setActiveUsers(users.filter((item) => item.isActive).length);
      setTotalRoles(roles.length);
      setAdminCount(
        users.filter((item) =>
          normalizeRoles(item).some(
            (role) => roleName(role).toLowerCase() === "admin"
          )
        ).length
      );
    } catch (requestError) {
      // Bỏ qua lỗi do abort (component unmount)
      if (requestError?.name === "CanceledError" || requestError?.code === "ERR_CANCELED") {
        return;
      }
      setError(
        requestError?.response?.data?.message ||
          requestError?.message ||
          "Failed to load dashboard summary."
      );
    } finally {
      if (!signal?.aborted) {
        setLoading(false);
      }
    }
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    loadSummary(controller.signal);

    return () => {
      controller.abort();
    };
  }, [loadSummary]);

  const stats = [
    { label: "Total Users", value: totalUsers, color: "#3b82f6" },
    { label: "Active Users", value: activeUsers, color: "#10b981" },
    { label: "Total Roles", value: totalRoles, color: "#8b5cf6" },
    { label: "Admins", value: adminCount, color: "#f59e0b" },
  ];

  return (
    <AdminShell>
      <div className="admin-dashboard-page">
        <header className="dashboard-header">
          <div>
            <h1>Dashboard</h1>
            <p className="welcome-text">
              Welcome back, <strong>{user?.name || user?.email || "Admin"}</strong>
            </p>
          </div>
        </header>

        <section className="attention-section" aria-label="Needs attention">
          <h2 className="section-title">Needs your attention</h2>
          <div className="attention-grid">
            {[
              { to: "/admin/events", label: "Events waiting for approval", value: pending.events },
              { to: "/admin/change-requests", label: "Postpone / reschedule requests", value: pending.changes },
              { to: "/admin/returns", label: "Ticket return requests", value: pending.returns },
            ].map((item) => (
              <Link
                key={item.to}
                to={item.to}
                className={`attention-card${item.value > 0 ? " has-work" : ""}`}
              >
                <span className="attention-count">{item.value ?? "—"}</span>
                <span className="attention-label">{item.label}</span>
                <span className="attention-go">{item.value > 0 ? "Review now →" : "All clear"}</span>
              </Link>
            ))}
          </div>
        </section>

        {error && (
          <div className="alert alert-error" role="alert">
            {error}
          </div>
        )}

        <section className="stats-grid">
          {stats.map((stat) => (
            <div
              key={stat.label}
              className="stat-card"
              style={{ borderLeftColor: stat.color }}
            >
              <span className="stat-label">{stat.label}</span>
              <span className="stat-value">
                {loading ? "..." : stat.value ?? "—"}
              </span>
            </div>
          ))}
        </section>

        {!loading && !error && totalUsers === 0 && (
          <p className="empty-state">No users found in the system.</p>
        )}
      </div>
    </AdminShell>
  );
  const handleLogout = () => {
    logout();
    navigate("/admin/login");
  };

  return <div className="admin-shell">
    <aside className="admin-sidebar">
      <div className="admin-brand"><div className="admin-brand-mark">CS</div><h2>ConcertShield</h2></div>
      <nav className="admin-nav" aria-label="Sidebar navigation">
        <button type="button" className="admin-nav-item active" onClick={() => navigate("/admin")}>Dashboard</button>
        <button type="button" className="admin-nav-item" onClick={() => navigate("/admin/users")}>User Management</button>
        <button type="button" className="admin-nav-item" onClick={() => navigate("/admin/roles")}>Role Management</button>
        <button type="button" className="admin-nav-item" onClick={() => navigate("/admin/events")}>Event Approvals</button>
        <button type="button" className="admin-nav-item" onClick={() => navigate("/admin/reviews")}>Review Management</button>
        <button type="button" className="admin-nav-item" onClick={() => navigate("/admin/wishlists")}>Wishlist Management</button>
        <button type="button" className="admin-nav-item" onClick={() => navigate("/admin/organizer-requests")}>Organizer Requests</button>
        <button type="button" className="admin-nav-item" onClick={() => navigate("/admin/vouchers")}>Voucher Management</button>
        <button type="button" className="admin-nav-item" onClick={() => navigate("/admin/queue")}>Virtual Queue</button>
        <button type="button" className="admin-nav-item" onClick={() => navigate("/admin/queue/abuse-alerts")}>Queue Abuse Alerts</button>
      </nav>
      <div className="admin-user-box"><span>Logged in as</span><strong>{user?.fullName || user?.email || "Admin"}</strong></div>
    </aside>

    <main className="admin-main">
      <header className="admin-topbar"><div className="topbar-title">Admin Panel</div><button type="button" className="admin-button" onClick={handleLogout}>Logout</button></header>
      {error && <div className="auth-message error">{error}<button type="button" onClick={() => setError("")}>Dismiss</button></div>}
      <section className="admin-header"><div><p>Admin dashboard</p><h1>System Management</h1></div></section>
      <section className="summary-grid" aria-label="Summary statistics">
        <div className="summary-card"><span className="label">Total users</span><div className="value">{totalUsers ?? "..."}</div><span className="trend">Registered accounts</span></div>
        <div className="summary-card"><span className="label">Active users</span><div className="value">{activeUsers ?? "..."}</div><span className="trend">{totalUsers ? `${Math.round((activeUsers / totalUsers) * 100)}% active` : "Loading"}</span></div>
        <div className="summary-card"><span className="label">Total roles</span><div className="value">{totalRoles ?? "..."}</div><span className="trend">{adminCount ?? "..."} administrators</span></div>
      </section>
    </main>
  </div>;
}
