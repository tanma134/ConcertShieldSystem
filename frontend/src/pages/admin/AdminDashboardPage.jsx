import { useEffect, useState, useCallback } from "react";
import AdminShell from "./AdminShell";
import { Link } from "react-router-dom";
import useAdminPending from "../../hooks/useAdminPending";
import adminApi from "../../api/adminApi";
import voucherApi from "../../api/voucherApi";
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
  const [totalVouchers, setTotalVouchers] = useState(null);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);

  const loadSummary = useCallback(async (signal) => {
    setLoading(true);
    setError("");
    try {
      const [usersResponse, rolesResponse, vouchersResponse] = await Promise.allSettled([
        adminApi.getUsers(signal),
        adminApi.getRoles(signal),
        voucherApi.getVouchers({ limit: 1 }),
      ]);

      if (signal?.aborted) return;

      const users = usersResponse.status === "fulfilled" ? (usersResponse.value?.data || []) : [];
      const roles = rolesResponse.status === "fulfilled" ? (rolesResponse.value?.data || []) : [];

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

      if (vouchersResponse.status === "fulfilled") {
        const vData = vouchersResponse.value?.data?.data;
        const vCount = vData?.total_count ?? vData?.totalCount ?? (Array.isArray(vData?.items) ? vData.items.length : 0);
        setTotalVouchers(vCount);
      }
    } catch (requestError) {
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
    { label: "Active Users", value: activeUsers, color: "#0e9e78" },
    { label: "Total Roles", value: totalRoles, color: "#8b5cf6" },
    { label: "System Vouchers", value: totalVouchers, color: "#ec4899" },
    { label: "Admins", value: adminCount, color: "#d98f14" },
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

        <section className="attention-section" aria-label="Quick management shortcuts" style={{ marginTop: "1.5rem" }}>
          <h2 className="section-title">Quick Management Portals</h2>
          <div className="attention-grid">
            <Link to="/admin/vouchers" className="attention-card has-work">
              <span className="attention-count" style={{ fontSize: "1.75rem" }}>🎟️ {totalVouchers !== null ? totalVouchers : ""}</span>
              <span className="attention-label" style={{ fontWeight: 600 }}>Voucher Management</span>
              <span className="attention-go">Create & Manage System Vouchers →</span>
            </Link>
            <Link to="/admin/organizer-requests" className="attention-card">
              <span className="attention-count" style={{ fontSize: "1.75rem" }}>🏢</span>
              <span className="attention-label" style={{ fontWeight: 600 }}>Organizer Requests</span>
              <span className="attention-go">Review Applications →</span>
            </Link>
            <Link to="/admin/policies" className="attention-card">
              <span className="attention-count" style={{ fontSize: "1.75rem" }}>📜</span>
              <span className="attention-label" style={{ fontWeight: 600 }}>Platform Policy</span>
              <span className="attention-go">Fee & Payout Terms →</span>
            </Link>
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
                {loading ? <span className="app-loading-spinner" role="status" aria-label="Loading" /> : stat.value ?? "—"}
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
}
