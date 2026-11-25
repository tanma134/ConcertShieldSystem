import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import OrganizerShell from "./OrganizerShell";
import { useAuth } from "../../context/AuthContext";
import eventApi from "../../api/eventApi";
import { formatDateRange, formatPrice } from "../../utils/format";
import "./MyConcertsPage.css";
import "./OrganizerStaffPage.css";

const STATUS_LABELS = {
  Draft: "Draft",
  Pending: "Pending Review",
  Published: "Published",
  Postponed: "Postponed",
  Rejected: "Rejected",
  Cancelled: "Cancelled",
};

const STATUS_CLASS = {
  Draft: "mc-status-draft",
  Pending: "mc-status-pending",
  Published: "mc-status-published",
  Rejected: "mc-status-rejected",
  Cancelled: "mc-status-cancelled",
};

export default function MyConcertsPage() {
  const { isOrganizer } = useAuth();
  const [events, setEvents] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [deletingId, setDeletingId] = useState(null);
  const [confirmModal, setConfirmModal] = useState(null);

  useEffect(() => {
    setLoading(true);
    eventApi
      .getMine()
      .then((res) => setEvents(res.data?.data || []))
      .catch(() => setError("Could not load your events list."))
      .finally(() => setLoading(false));
  }, []);

  const handleDeleteDraft = (event) => {
    event.preventDefault();
    event.stopPropagation();
    const eventId = Number(event.currentTarget.dataset.eventId);
    const title = event.currentTarget.dataset.eventTitle;
    setConfirmModal({
      title: "Delete Event",
      message: `Delete event "${title}"? This action cannot be undone.`,
      confirmText: "Delete Event",
      confirmBtnStyle: { background: "#dc2626", color: "#ffffff" },
      onConfirm: async () => {
        setConfirmModal(null);
        setDeletingId(eventId);
        setError("");
        try {
          await eventApi.remove(eventId);
          setEvents((items) => items.filter((item) => item.eventId !== eventId));
        } catch (err) {
          setError(err.response?.data?.message || "Could not delete this event.");
        } finally {
          setDeletingId(null);
        }
      }
    });
  };

  return (
    <OrganizerShell title="My Concerts">

      <div className="tb-container mc-wrap">
        <div className="mc-head">
          <h1>My Events</h1>
          {isOrganizer && (
            <Link to="/organizer/events/new" className="tb-btn tb-btn-primary">
              + Create New Event
            </Link>
          )}
        </div>

        {loading && <div className="tb-loading">Loading...</div>}
        {!loading && error && <div className="tb-error">{error}</div>}
        {!loading && !error && events.length === 0 && (
          <div className="tb-empty">
            You haven't created any events yet.{" "}
            <Link to="/organizer/events/new">Create your first event</Link>
          </div>
        )}

        {!loading && events.length > 0 && (
          <div className="mc-list">
            {events.map((ev) => (
              <article key={ev.eventId} className="mc-card">
                <Link
                  to={`/organizer/events/${ev.eventId}/edit`}
                  className="mc-card-link"
                >
                <div className="mc-card-poster">
                  {ev.posterUrl ? (
                    <img src={ev.posterUrl} alt={ev.title} />
                  ) : (
                    <div className="mc-card-poster-placeholder">No poster</div>
                  )}
                </div>
                <div className="mc-card-body">
                  <div className="mc-card-top">
                    <h3>{ev.title}</h3>
                    <span className={"mc-status " + (STATUS_CLASS[ev.status] || "")}>
                      {STATUS_LABELS[ev.status] || ev.status}
                    </span>
                  </div>
                  <p className="mc-card-date">{formatDateRange(ev.startsAt, ev.endsAt)}</p>
                  <p className="mc-card-meta">
                    {ev.city || "No city"} ·{" "}
                    {ev.minPrice != null ? `From ${formatPrice(ev.minPrice)}` : "No price"}
                  </p>
                </div>
                </Link>

                <div style={{ display: "flex", gap: "8px", marginTop: "8px", flexWrap: "wrap" }}>
                  <Link
                    to={`/organizer/events/${ev.eventId}/orders`}
                    className="co-button co-button-primary mc-view-orders"
                    style={{ flex: 1, minWidth: "120px" }}
                  >
                    View Orders
                  </Link>
                  <Link
                    to={`/organizer/events/${ev.eventId}/realtime-checkin`}
                    className="co-button"
                    style={{
                      flex: 1,
                      minWidth: "120px",
                      background: "#059669",
                      color: "#fff",
                      textAlign: "center",
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "center",
                      textDecoration: "none",
                      fontSize: "13px",
                      fontWeight: 600,
                      borderRadius: "6px",
                      padding: "8px 12px",
                    }}
                  >
                    📡 Live Check-in
                  </Link>
                </div>

                {["Draft", "Rejected"].includes(ev.status) && <div className="mc-config-links">
                  <Link to={`/organizer/events/${ev.eventId}/compliance`}>Compliance</Link>
                  <Link to={`/organizer/events/${ev.eventId}/changes`}>Schedule changes</Link>
                  <Link to={`/organizer/events/${ev.eventId}/seating`}>Seating</Link>
                  <Link to={`/organizer/events/${ev.eventId}/pricing`}>Pricing</Link>
                  <Link to={`/organizer/events/${ev.eventId}/refunds`}>Refund policies</Link>
                </div>}

                {ev.status === "Published" && (
                  <div className="mc-config-links">
                    <Link to={`/organizer/events/${ev.eventId}/checkin`}>Check-in &amp; Staff</Link>
                    <Link to={`/organizer/events/${ev.eventId}/revenue`}>Revenue</Link>
                    <Link to={`/organizer/events/${ev.eventId}/seating`}>Seating</Link>
                    <Link to={`/organizer/events/${ev.eventId}/pricing`}>Pricing</Link>
                    <Link to={`/organizer/events/${ev.eventId}/refunds`}>Refunds</Link>
                  </div>
                )}
                {["Draft", "Rejected"].includes(ev.status) && (
                  <button
                    type="button"
                    className="mc-delete-draft"
                    data-event-id={ev.eventId}
                    data-event-title={ev.title}
                    onClick={handleDeleteDraft}
                    disabled={deletingId === ev.eventId}
                  >
                    {deletingId === ev.eventId ? "Deleting..." : `Delete ${ev.status.toLowerCase()}`}
                  </button>
                )}
              </article>
            ))}
          </div>
        )}
      </div>

      {/* Confirmation Modal */}
      {confirmModal && (
        <div className="osp-modal-overlay" onClick={() => setConfirmModal(null)}>
          <div className="osp-modal-box" onClick={(e) => e.stopPropagation()} style={{ maxWidth: "460px" }}>
            <div className="osp-modal-header">
              <h2 style={{ fontSize: "1.2rem", fontWeight: 700, color: "#111827", margin: 0 }}>
                {confirmModal.title}
              </h2>
              <button type="button" className="osp-modal-close" onClick={() => setConfirmModal(null)}>
                ×
              </button>
            </div>
            <div className="osp-modal-body" style={{ padding: "18px 20px" }}>
              <p style={{ margin: 0, color: "#4b5563", fontSize: "0.95rem", lineHeight: "1.55" }}>
                {confirmModal.message}
              </p>
            </div>
            <div className="osp-modal-footer" style={{ display: "flex", justifyContent: "flex-end", gap: "10px", padding: "14px 20px" }}>
              <button
                type="button"
                className="tb-btn tb-btn-outline"
                onClick={() => setConfirmModal(null)}
                style={{ padding: "8px 18px", borderRadius: "8px", border: "1px solid #d1d5db", background: "#fff", color: "#374151", cursor: "pointer", fontWeight: 600 }}
              >
                Cancel
              </button>
              <button
                type="button"
                onClick={confirmModal.onConfirm}
                style={{
                  padding: "8px 20px",
                  borderRadius: "8px",
                  fontWeight: 600,
                  fontSize: "0.9rem",
                  border: "none",
                  cursor: "pointer",
                  boxShadow: "0 2px 4px rgba(0,0,0,0.15)",
                  ...(confirmModal.confirmBtnStyle || { background: "linear-gradient(135deg, #d61f6e, #be185d)", color: "#ffffff" })
                }}
              >
                {confirmModal.confirmText || "Confirm"}
              </button>
            </div>
          </div>
        </div>
      )}

    </OrganizerShell>
  );
}
