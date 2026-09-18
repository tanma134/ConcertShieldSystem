import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import reviewApi from "../../api/reviewApi";
import { useAuth } from "../../context/AuthContext";
import "./AdminDashboardPage.css";
import "./ReviewManagementPage.css";

const stars = (rating) => "★".repeat(rating) + "☆".repeat(5 - rating);

export default function ReviewManagementPage() {
  const navigate = useNavigate();
  const { user, logout } = useAuth();
  const [reviews, setReviews] = useState([]);
  const [filters, setFilters] = useState({ eventName: "", rating: "", status: "all", page: 1 });
  const [paging, setPaging] = useState({ page: 1, pageSize: 10, totalPages: 1 });
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [pendingVisibility, setPendingVisibility] = useState(null);
  const [replyTarget, setReplyTarget] = useState(null);
  const [reply, setReply] = useState("");
  const [sending, setSending] = useState(false);

  const loadReviews = async (nextFilters = filters) => {
    try {
      setLoading(true); setError("");
      const response = await reviewApi.getAdmin({ eventName: nextFilters.eventName || undefined, rating: nextFilters.rating || undefined, status: nextFilters.status === "all" ? undefined : nextFilters.status, page: nextFilters.page, pageSize: paging.pageSize });
      const data = response.data || {};
      setReviews(data.items || []); setPaging((current) => ({ ...current, page: data.page || nextFilters.page, totalPages: data.totalPages || 1 }));
    } catch (requestError) { setError(requestError.response?.data?.message || "Failed to load reviews."); }
    finally { setLoading(false); }
  };

  // The effect owns the async request lifecycle for the current filters.
  // eslint-disable-next-line react-hooks/set-state-in-effect
  useEffect(() => { loadReviews(); }, [filters.page, filters.status, filters.rating]);

  const updateFilter = (name, value) => setFilters((current) => ({ ...current, [name]: value, page: 1 }));
  const search = (event) => { event.preventDefault(); loadReviews({ ...filters, page: 1 }); };

  const confirmVisibility = async () => {
    if (!pendingVisibility) return;
    try { await reviewApi.setVisibility(pendingVisibility.reviewId, !pendingVisibility.isDeleted); setSuccess(pendingVisibility.isDeleted ? "Review shown successfully." : "Review hidden successfully."); setPendingVisibility(null); await loadReviews(); }
    catch (requestError) { setError(requestError.response?.data?.message || "Failed to update review visibility."); }
  };

  const sendReply = async (event) => {
    event.preventDefault();
    if (!reply.trim() || !replyTarget) return;
    try { setSending(true); await reviewApi.reply(replyTarget.reviewId, reply.trim()); setSuccess("Reply posted successfully."); setReply(""); setReplyTarget(null); await loadReviews(); }
    catch (requestError) { setError(requestError.response?.data?.message || "Failed to post reply."); }
    finally { setSending(false); }
  };

  const nav = (path) => navigate(path);
  return <div className="admin-shell"><aside className="admin-sidebar"><div className="admin-brand"><div className="admin-brand-mark">CS</div><h2>ConcertShield</h2></div><nav className="admin-nav" aria-label="Sidebar navigation"><button type="button" className="admin-nav-item" onClick={() => nav("/admin")}>Dashboard</button><button type="button" className="admin-nav-item" onClick={() => nav("/admin/users")}>User Management</button><button type="button" className="admin-nav-item" onClick={() => nav("/admin/roles")}>Role Management</button><button type="button" className="admin-nav-item active" onClick={() => nav("/admin/reviews")}>Review Management</button><button type="button" className="admin-nav-item" onClick={() => nav("/admin/organizer-requests")}>Organizer Requests</button></nav><div className="admin-user-box"><span>Logged in as</span><strong>{user?.fullName || user?.email || "Admin"}</strong></div></aside><main className="admin-main"><header className="admin-topbar"><div className="topbar-title">Admin Panel</div><button type="button" className="admin-button" onClick={() => { logout(); navigate("/admin/login"); }}>Logout</button></header><section className="admin-header review-admin-header"><div><p>Moderation</p><h1>Review Management</h1></div></section>{error && <div className="auth-message error">{error}<button type="button" onClick={() => setError("")}>Dismiss</button></div>}{success && <div className="auth-message success">{success}<button type="button" onClick={() => setSuccess("")}>Dismiss</button></div>}<section className="admin-panel"><form className="review-admin-toolbar" onSubmit={search}><input type="search" value={filters.eventName} onChange={(event) => setFilters((current) => ({ ...current, eventName: event.target.value }))} placeholder="Search event..." /><select value={filters.rating} onChange={(event) => updateFilter("rating", event.target.value)}><option value="">All Ratings</option>{[5, 4, 3, 2, 1].map((rating) => <option key={rating} value={rating}>{stars(rating)}</option>)}</select><select value={filters.status} onChange={(event) => updateFilter("status", event.target.value)}><option value="all">All Status</option><option value="active">Active</option><option value="hidden">Hidden</option></select><button type="submit" className="admin-button-secondary">Search</button></form>{loading ? <div className="empty-state">Loading reviews...</div> : reviews.length === 0 ? <div className="empty-state">No reviews found.</div> : <div className="table-wrap"><table className="user-table review-admin-table"><thead><tr><th>ID</th><th>Event</th><th>Customer</th><th>Rating</th><th>Comment</th><th>Replies</th><th>Status</th><th>Created At</th><th>Actions</th></tr></thead><tbody>{reviews.map((review) => <tr key={review.reviewId}><td>#{review.reviewId}</td><td>{review.eventName || `Event #${review.eventId}`}</td><td>{review.userName || `User #${review.userId}`}</td><td><span className="review-admin-rating">{stars(review.rating)}</span></td><td className="review-comment-cell" title={review.comment || ""}>{review.comment || "-"}</td><td>{review.replies?.length || 0}</td><td><span className={`status-pill ${review.isDeleted ? "inactive" : "active"}`}>{review.isDeleted ? "Hidden" : "Active"}</span></td><td>{new Date(review.createdAt).toLocaleDateString()}</td><td><div className="table-actions"><button type="button" className="text-button" onClick={() => setReplyTarget(review)}>Reply</button><button type="button" className="danger-button" onClick={() => setPendingVisibility(review)}>{review.isDeleted ? "Show" : "Hide"}</button></div></td></tr>)}</tbody></table></div>}<div className="review-pagination"><button type="button" className="admin-button-secondary" disabled={paging.page <= 1 || loading} onClick={() => setFilters((current) => ({ ...current, page: current.page - 1 }))}>Previous</button><span>Page {paging.page} of {paging.totalPages}</span><button type="button" className="admin-button-secondary" disabled={paging.page >= paging.totalPages || loading} onClick={() => setFilters((current) => ({ ...current, page: current.page + 1 }))}>Next</button></div></section></main>{pendingVisibility && <div className="modal-overlay" onClick={() => setPendingVisibility(null)}><div className="modal-card small" onClick={(event) => event.stopPropagation()}><div className="modal-header"><h3>{pendingVisibility.isDeleted ? "Show Review" : "Hide Review"}</h3><button type="button" className="icon-button" onClick={() => setPendingVisibility(null)}>x</button></div><div className="modal-body"><p>Are you sure you want to {pendingVisibility.isDeleted ? "show" : "hide"} this review?</p></div><div className="modal-footer"><button type="button" className="admin-button-secondary" onClick={() => setPendingVisibility(null)}>Cancel</button><button type="button" className="admin-button" onClick={confirmVisibility}>{pendingVisibility.isDeleted ? "Show Review" : "Hide Review"}</button></div></div></div>}{replyTarget && <div className="modal-overlay" onClick={() => setReplyTarget(null)}><div className="modal-card" onClick={(event) => event.stopPropagation()}><div className="modal-header"><h3>Reply to Review</h3><button type="button" className="icon-button" onClick={() => setReplyTarget(null)}>x</button></div><div className="modal-body"><div className="review-reply-context"><strong>{replyTarget.userName || `User #${replyTarget.userId}`}</strong><span className="review-admin-rating">{stars(replyTarget.rating)}</span><p>{replyTarget.comment || "No comment"}</p></div><form onSubmit={sendReply} className="review-reply-form"><textarea value={reply} maxLength={1000} onChange={(event) => setReply(event.target.value)} placeholder="Write a reply..." rows="5" required /><div className="modal-footer"><button type="button" className="admin-button-secondary" onClick={() => setReplyTarget(null)}>Cancel</button><button type="submit" className="admin-button" disabled={sending}>{sending ? "Sending..." : "Send Reply"}</button></div></form></div></div></div>}</div>;
}
