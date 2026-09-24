import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import adminApi from "../../api/adminApi";
import { useAuth } from "../../context/AuthContext";
import "./AdminDashboardPage.css";

export default function RoleManagementPage() {
  const navigate = useNavigate();
  const { user, logout } = useAuth();
  const [roles, setRoles] = useState([]);
  const [search, setSearch] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [selectedRole, setSelectedRole] = useState(null);
  const [roleName, setRoleName] = useState("");
  const [modal, setModal] = useState(null);

  const loadRoles = async () => {
    try { setLoading(true); const response = await adminApi.getRoles(); setRoles(response.data || []); }
    catch (requestError) { setError(requestError.response?.data?.message || "Failed to load roles."); }
    finally { setLoading(false); }
  };
  useEffect(() => { loadRoles(); }, []);
  const filteredRoles = useMemo(() => { const keyword = search.trim().toLowerCase(); return keyword ? roles.filter((role) => String(role.roleName || "").toLowerCase().includes(keyword) || String(role.roleId).includes(keyword)) : roles; }, [roles, search]);
  const closeModal = () => { setModal(null); setSelectedRole(null); setRoleName(""); };
  const saveRole = async (event) => { event.preventDefault(); const value = roleName.trim(); if (!value) return setError("Role name is required."); try { if (selectedRole) await adminApi.updateRole(selectedRole.roleId, { roleName: value }); else await adminApi.createRole({ roleName: value }); setSuccess(selectedRole ? "Role updated successfully." : "Role created successfully."); closeModal(); await loadRoles(); } catch (requestError) { setError(requestError.response?.data?.message || "Role operation failed."); } };
  const deleteRole = async () => { try { await adminApi.deleteRole(selectedRole.roleId); setSuccess("Role deleted successfully."); closeModal(); await loadRoles(); } catch (requestError) { setError(requestError.response?.data?.message || "Unable to delete role."); } };

  return <div className="admin-shell"><aside className="admin-sidebar"><div className="admin-brand"><div className="admin-brand-mark">CS</div><h2>ConcertShield</h2></div><nav className="admin-nav" aria-label="Sidebar navigation"><button type="button" className="admin-nav-item" onClick={() => navigate("/admin")}>Dashboard</button><button type="button" className="admin-nav-item" onClick={() => navigate("/admin/users")}>User Management</button><button type="button" className="admin-nav-item active" onClick={() => navigate("/admin/roles")}>Role Management</button><button type="button" className="admin-nav-item" onClick={() => navigate("/admin/reviews")}>Review Management</button><button type="button" className="admin-nav-item" onClick={() => navigate("/admin/organizer-requests")}>Organizer Requests</button></nav><div className="admin-user-box"><span>Logged in as</span><strong>{user?.fullName || user?.email || "Admin"}</strong></div></aside><main className="admin-main"><header className="admin-topbar"><div className="topbar-title">Admin Panel</div><button type="button" className="admin-button" onClick={() => { logout(); navigate("/admin/login"); }}>Logout</button></header>{error && <div className="auth-message error">{error}<button type="button" onClick={() => setError("")}>Dismiss</button></div>}{success && <div className="auth-message success">{success}<button type="button" onClick={() => setSuccess("")}>Dismiss</button></div>}<section className="admin-panel"><div className="panel-header"><h2>Role Management</h2><button type="button" className="admin-button" onClick={() => { setRoleName(""); setModal("edit"); }}>Create Role</button></div><div className="toolbar-row"><div className="search-box"><span>Search</span><input type="search" value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search roles..." /></div></div>{loading ? <div className="empty-state">Loading roles...</div> : filteredRoles.length === 0 ? <div className="empty-state">No roles found.</div> : <div className="table-wrap"><table className="user-table"><thead><tr><th>ID</th><th>Role Name</th><th>Actions</th></tr></thead><tbody>{filteredRoles.map((role) => <tr key={role.roleId}><td>#{role.roleId}</td><td>{role.roleName}</td><td><div className="table-actions"><button type="button" className="text-button" onClick={() => { setSelectedRole(role); setRoleName(role.roleName || ""); setModal("edit"); }}>Edit</button><button type="button" className="danger-button" onClick={() => { setSelectedRole(role); setModal("delete"); }}>Delete</button></div></td></tr>)}</tbody></table></div>}</section></main>{modal && <div className="modal-overlay" onClick={closeModal}><div className="modal-card small" onClick={(event) => event.stopPropagation()}><div className="modal-header"><h3>{modal === "delete" ? "Delete Role" : selectedRole ? "Edit Role" : "Create Role"}</h3><button type="button" className="icon-button" onClick={closeModal}>x</button></div>{modal === "delete" ? <><div className="modal-body"><p>Are you sure you want to delete this role?</p></div><div className="modal-footer"><button type="button" className="admin-button-secondary" onClick={closeModal}>Cancel</button><button type="button" className="admin-button danger" onClick={deleteRole}>Delete</button></div></> : <form onSubmit={saveRole} className="modal-body form-grid"><label className="full-width"><span>Role Name</span><input value={roleName} onChange={(event) => setRoleName(event.target.value)} required /></label><div className="modal-footer full-width"><button type="button" className="admin-button-secondary" onClick={closeModal}>Cancel</button><button type="submit" className="admin-button">{selectedRole ? "Save Changes" : "Create Role"}</button></div></form>}</div></div>}</div>;
}
