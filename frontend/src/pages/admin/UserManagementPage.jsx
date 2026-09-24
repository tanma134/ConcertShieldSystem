import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import adminApi from "../../api/adminApi";
import { useAuth } from "../../context/AuthContext";
import "./AdminDashboardPage.css";

const formatDate = (value) => {
  if (!value) return "N/A";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "N/A" : date.toLocaleDateString("en-GB");
};
const normalizeRoles = (user) => {
  if (!user) return [];
  if (Array.isArray(user.roles)) return user.roles;
  if (user.roleName) return [{ roleName: user.roleName, roleId: user.roleId || 0 }];
  return [];
};
const roleName = (role) => String(role?.roleName || role || "");

export default function UserManagementPage() {
  const navigate = useNavigate();
  const { user, logout } = useAuth();
  const [users, setUsers] = useState([]);
  const [roles, setRoles] = useState([]);
  const [search, setSearch] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [selectedUser, setSelectedUser] = useState(null);
  const [pendingStatusUser, setPendingStatusUser] = useState(null);
  const [modal, setModal] = useState(null);

  const load = async () => {
    try {
      setLoading(true); setError("");
      const [usersResponse, rolesResponse] = await Promise.all([adminApi.getUsers(), adminApi.getRoles()]);
      setUsers(usersResponse.data || []); setRoles(rolesResponse.data || []);
    } catch (requestError) { setError(requestError.response?.data?.message || "Failed to load users."); }
    finally { setLoading(false); }
  };
  useEffect(() => { load(); }, []);

  const filteredUsers = useMemo(() => {
    const keyword = search.trim().toLowerCase();
    if (!keyword) return users;
    return users.filter((item) => [item.fullName, item.email, item.phoneNumber, ...normalizeRoles(item).map(roleName)].some((value) => String(value || "").toLowerCase().includes(keyword)));
  }, [users, search]);
  const availableRoles = roles.filter((role) => !["admin", "organizer"].includes(roleName(role).toLowerCase()));

  const closeModal = () => { setModal(null); setSelectedUser(null); setPendingStatusUser(null); };
  const openDetails = async (userId) => {
    try { setModal("details"); setSelectedUser(null); const response = await adminApi.getUserById(userId); setSelectedUser(response.data); }
    catch (requestError) { setError(requestError.response?.data?.message || "Failed to load user details."); }
  };
  const openStatus = (item) => {
    if (normalizeRoles(item).some((role) => roleName(role).toLowerCase() === "admin")) return setError("Admin accounts cannot be blocked or unblocked.");
    setPendingStatusUser(item); setModal("status");
  };
  const updateUser = async (event) => {
    event.preventDefault();
    const formData = new FormData(event.currentTarget);
    try {
      await adminApi.updateUser(selectedUser.userId, { email: selectedUser.email, fullName: formData.get("fullName"), phoneNumber: formData.get("phoneNumber"), avatarUrl: formData.get("avatarUrl"), roleNames: formData.getAll("roleNames") });
      setSuccess("User updated successfully."); closeModal(); await load();
    } catch (requestError) { setError(requestError.response?.data?.message || "User update failed."); }
  };
  const confirmStatus = async () => {
    try { await adminApi.updateUserStatus(pendingStatusUser.userId, pendingStatusUser.isActive ? 2 : 1); setSuccess(pendingStatusUser.isActive ? "User blocked successfully." : "User unblocked successfully."); closeModal(); await load(); }
    catch (requestError) { setError(requestError.response?.data?.message || "Failed to update user status."); }
  };
  const assignRole = async (roleId) => {
    try { await adminApi.assignRole(selectedUser.userId, roleId); setSuccess("Role assigned successfully."); closeModal(); await load(); }
    catch (requestError) { setError(requestError.response?.data?.message || "Failed to assign role."); }
  };

  return <div className="admin-shell"><aside className="admin-sidebar"><div className="admin-brand"><div className="admin-brand-mark">CS</div><h2>ConcertShield</h2></div><nav className="admin-nav" aria-label="Sidebar navigation"><button type="button" className="admin-nav-item" onClick={() => navigate("/admin")}>Dashboard</button><button type="button" className="admin-nav-item active" onClick={() => navigate("/admin/users")}>User Management</button><button type="button" className="admin-nav-item" onClick={() => navigate("/admin/roles")}>Role Management</button><button type="button" className="admin-nav-item" onClick={() => navigate("/admin/reviews")}>Review Management</button><button type="button" className="admin-nav-item" onClick={() => navigate("/admin/organizer-requests")}>Organizer Requests</button></nav><div className="admin-user-box"><span>Logged in as</span><strong>{user?.fullName || user?.email || "Admin"}</strong></div></aside><main className="admin-main"><header className="admin-topbar"><div className="topbar-title">Admin Panel</div><button type="button" className="admin-button" onClick={() => { logout(); navigate("/admin/login"); }}>Logout</button></header>{error && <div className="auth-message error">{error}<button type="button" onClick={() => setError("")}>Dismiss</button></div>}{success && <div className="auth-message success">{success}<button type="button" onClick={() => setSuccess("")}>Dismiss</button></div>}<section className="admin-panel"><div className="panel-header"><h2>User Management</h2><button type="button" className="admin-button-secondary" onClick={load}>Refresh</button></div><div className="toolbar-row"><div className="search-box"><span>Search</span><input type="search" value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Name, Email, Phone..." /></div></div>{loading ? <div className="empty-state">Loading users...</div> : filteredUsers.length === 0 ? <div className="empty-state">No users found.</div> : <div className="table-wrap"><table className="user-table"><thead><tr><th>ID</th><th>Full Name</th><th>Email</th><th>Phone</th><th>Roles</th><th>Status</th><th>Created At</th><th>Actions</th></tr></thead><tbody>{filteredUsers.map((item) => { const itemRoles = normalizeRoles(item); const isAdmin = itemRoles.some((role) => roleName(role).toLowerCase() === "admin"); const isProtected = itemRoles.some((role) => ["admin", "organizer"].includes(roleName(role).toLowerCase())); return <tr key={item.userId}><td>#{item.userId}</td><td>{item.fullName || "N/A"}</td><td>{item.email}</td><td>{item.phoneNumber || "N/A"}</td><td><div className="role-stack">{itemRoles.length ? itemRoles.map((role, index) => <span className="role-pill" key={`${item.userId}-${index}`}>{roleName(role)}</span>) : <span className="role-pill muted">No role</span>}</div></td><td><span className={`status-pill ${item.isActive ? "active" : "inactive"}`}>{item.isActive ? "Active" : "Blocked"}</span></td><td>{formatDate(item.createdAt)}</td><td><div className="table-actions"><button type="button" className="text-button" onClick={() => openDetails(item.userId)}>Details</button><button type="button" className="text-button" onClick={() => { setSelectedUser(item); setModal("edit"); }}>Update</button><button type="button" className="text-button" disabled={isAdmin} onClick={() => openStatus(item)}>{item.isActive ? "Block" : "Unblock"}</button><button type="button" className="text-button" disabled={isProtected} onClick={() => { setSelectedUser(item); setModal("assign"); }}>Assign Role</button></div></td></tr>; })}</tbody></table></div>}</section></main>{modal && <div className="modal-overlay" onClick={closeModal}><div className={`modal-card ${modal === "status" ? "small" : ""}`} onClick={(event) => event.stopPropagation()}><div className="modal-header"><h3>{modal === "details" ? "User Details" : modal === "edit" ? "Update User" : modal === "assign" ? "Assign Role" : pendingStatusUser?.isActive ? "Block User" : "Unblock User"}</h3><button type="button" className="icon-button" onClick={closeModal}>x</button></div>{modal === "details" && <><div className="modal-body info-grid">{[["User ID", `#${selectedUser?.userId || "..."}`], ["Full Name", selectedUser?.fullName || "N/A"], ["Email", selectedUser?.email || "..."], ["Phone Number", selectedUser?.phoneNumber || "N/A"], ["Roles", normalizeRoles(selectedUser).map(roleName).join(", ") || "No role"], ["Account Status", selectedUser?.isActive ? "Active" : "Blocked"], ["Created At", formatDate(selectedUser?.createdAt)]].map(([label, value]) => <div key={label}><span>{label}</span><strong>{value}</strong></div>)}</div><div className="modal-footer"><button type="button" className="admin-button-secondary" onClick={closeModal}>Close</button></div></>}{modal === "edit" && <form onSubmit={updateUser} className="modal-body form-grid"><label><span>Full Name</span><input name="fullName" defaultValue={selectedUser?.fullName || ""} required /></label><label><span>Phone Number</span><input name="phoneNumber" defaultValue={selectedUser?.phoneNumber || ""} /></label><label><span>Avatar URL</span><input name="avatarUrl" defaultValue={selectedUser?.avatarUrl || ""} /></label><label><span>Email</span><input value={selectedUser?.email || ""} readOnly /></label><div className="full-width"><span className="field-label">Roles</span><div className="role-check-list">{availableRoles.map((role) => <label key={role.roleId} className="role-check-item"><input type="checkbox" name="roleNames" value={roleName(role)} defaultChecked={normalizeRoles(selectedUser).map(roleName).includes(roleName(role))} /><span>{roleName(role)}</span></label>)}</div></div><div className="modal-footer full-width"><button type="button" className="admin-button-secondary" onClick={closeModal}>Cancel</button><button type="submit" className="admin-button">Save Changes</button></div></form>}{modal === "status" && <><div className="modal-body"><p>Are you sure you want to {pendingStatusUser?.isActive ? "block" : "unblock"} this user?</p></div><div className="modal-footer"><button type="button" className="admin-button-secondary" onClick={closeModal}>Cancel</button><button type="button" className="admin-button" onClick={confirmStatus}>Confirm</button></div></>}{modal === "assign" && <><div className="modal-body"><p><strong>User:</strong> {selectedUser?.fullName || selectedUser?.email}</p><div className="assign-role-list">{availableRoles.filter((role) => !normalizeRoles(selectedUser).map(roleName).includes(roleName(role))).map((role) => <button type="button" className="role-option" key={role.roleId} onClick={() => assignRole(role.roleId)}>{roleName(role)}</button>)}</div></div><div className="modal-footer"><button type="button" className="admin-button-secondary" onClick={closeModal}>Close</button></div></>}</div></div>}</div>;
}
