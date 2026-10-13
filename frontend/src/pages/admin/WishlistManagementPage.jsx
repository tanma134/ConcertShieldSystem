import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import wishlistApi from "../../api/wishlistApi";
import { useAuth } from "../../context/AuthContext";
import "./AdminDashboardPage.css";
import "./ReviewManagementPage.css";
import AdminShell from "./AdminShell";

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

  return <><AdminShell title="Wishlist Management"><section className="admin-header review-admin-header"><div><p>Engagement</p><h1>Wishlist Management</h1></div></section>{error && <div className="auth-message error">{error}<button type="button" onClick={() => setError("")}>Dismiss</button></div>}<section className="admin-panel"><form className="review-admin-toolbar" onSubmit={submitSearch}><input type="search" value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search event..." /><select value={sort} onChange={(event) => setSort(event.target.value)}><option value="count-desc">Most Wishlisted</option><option value="count-asc">Least Wishlisted</option><option value="name-asc">A-Z</option><option value="name-desc">Z-A</option></select><button type="submit" className="admin-button-secondary">Search</button></form>{loading ? <div className="empty-state">Loading wishlist statistics...</div> : items.length === 0 ? <div className="empty-state">No events found.</div> : <div className="table-wrap"><table className="user-table"><thead><tr><th>Event</th><th>Status</th><th>Wishlist Count</th></tr></thead><tbody>{items.map((item) => <tr key={item.eventId}><td>{item.eventName}</td><td><span className="status-pill active">{item.status}</span></td><td><strong>{item.wishlistCount}</strong></td></tr>)}</tbody></table></div>}</section></AdminShell></>;
}
