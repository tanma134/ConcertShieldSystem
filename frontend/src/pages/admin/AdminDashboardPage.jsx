import { useEffect, useState, useCallback } from "react";
import AdminShell from "./AdminShell";
import { useNavigate } from "react-router-dom";
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
  const navigate = useNavigate();
  const { user, logout } = useAuth();

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

  const handleLogout = () => {
    logout();
    navigate("/admin/login");
  };

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
          <button className="btn-logout" onClick={handleLogout}>
            Logout
          </button>
        </header>

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
}