import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import wishlistApi from "../../api/wishlistApi";
import { useAuth } from "../../context/AuthContext";
import "./AdminDashboardPage.css";
import "./ReviewManagementPage.css";

export default function WishlistManagementPage() {
  const navigate = useNavigate();
  const { user, logout } = useAuth();
  const [items, setItems] = useState([]);
  const [search, setSearch] = useState("");
  const [sort, setSort] = useState("count-desc");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const load = async (nextSearch = search, nextSort = sort) => {
    try { setLoading(true); setError(""); setItems(await wishlistApi.getAdminSummary({ search: nextSearch || undefined, sort: nextSort })); }
    catch (requestError) { setError(requestError.response?.data?.message || "Failed to load wishlist statistics."); }
    finally { setLoading(false); }
  };
  useEffect(() => { load(); }, [sort]);
  const submitSearch = (event) => { event.preventDefault(); load(search, sort); };

  return <div className="admin-shell"><aside className="admin-sidebar"><div className="admin-brand"><div className="admin-brand-mark">CS</div><h2>ConcertShield</h2></div><nav className="admin-nav" aria-label="Sidebar navigation"><button type="button" className="admin-nav-item" onClick={() => navigate("/admin")}>Dashboard</button><button type="button" className="admin-nav-item" onClick={() => navigate("/admin/users")}>User Management</button><button type="button" className="admin-nav-item" onClick={() => navigate("/admin/roles")}>Role Management</button><button type="button" className="admin-nav-item" onClick={() => navigate("/admin/reviews")}>Review Management</button><button type="button" className="admin-nav-item active" onClick={() => navigate("/admin/wishlists")}>Wishlist Management</button></nav><div className="admin-user-box"><span>Logged in as</span><strong>{user?.fullName || user?.email || "Admin"}</strong></div></aside><main className="admin-main"><header className="admin-topbar"><div className="topbar-title">Admin Panel</div><button type="button" className="admin-button" onClick={() => { logout(); navigate("/admin/login"); }}>Logout</button></header><section className="admin-header review-admin-header"><div><p>Engagement</p><h1>Wishlist Management</h1></div></section>{error && <div className="auth-message error">{error}<button type="button" onClick={() => setError("")}>Dismiss</button></div>}<section className="admin-panel"><form className="review-admin-toolbar" onSubmit={submitSearch}><input type="search" value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search event..." /><select value={sort} onChange={(event) => setSort(event.target.value)}><option value="count-desc">Most Wishlisted</option><option value="count-asc">Least Wishlisted</option><option value="name-asc">A-Z</option><option value="name-desc">Z-A</option></select><button type="submit" className="admin-button-secondary">Search</button></form>{loading ? <div className="empty-state">Loading wishlist statistics...</div> : items.length === 0 ? <div className="empty-state">No events found.</div> : <div className="table-wrap"><table className="user-table"><thead><tr><th>Event</th><th>Status</th><th>Wishlist Count</th></tr></thead><tbody>{items.map((item) => <tr key={item.eventId}><td>{item.eventName}</td><td><span className="status-pill active">{item.status}</span></td><td><strong>{item.wishlistCount}</strong></td></tr>)}</tbody></table></div>}</section></main></div>;
}
