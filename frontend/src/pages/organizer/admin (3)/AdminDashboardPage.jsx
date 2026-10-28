import { useEffect, useState } from "react";
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

  useEffect(() => {
    const loadSummary = async () => {
      try {
        const [usersResponse, rolesResponse] = await Promise.all([
          adminApi.getUsers(),
          adminApi.getRoles(),
        ]);
        const users = usersResponse.data || [];
        const roles = rolesResponse.data || [];
        setTotalUsers(users.length);
        setActiveUsers(users.filter((item) => item.isActive).length);
        setTotalRoles(roles.length);
        setAdminCount(users.filter((item) => normalizeRoles(item).some((role) => roleName(role).toLowerCase() === "admin")).length);
      } catch (requestError) {
        setError(requestError.response?.data?.message || "Failed to load dashboard summary.");
      }
    };

    loadSummary();
  }, []);

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
        <button type="button" className="admin-nav-item" onClick={() => navigate("/admin/reviews")}>Review Management</button>
        <button type="button" className="admin-nav-item" onClick={() => navigate("/admin/wishlists")}>Wishlist Management</button>
        <button type="button" className="admin-nav-item" onClick={() => navigate("/admin/organizer-requests")}>Organizer Requests</button>
        <button type="button" className="admin-nav-item" onClick={() => navigate("/admin/vouchers")}>Voucher Management</button>
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
