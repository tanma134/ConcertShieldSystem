import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import OrganizerShell from "./OrganizerShell";
import { useAuth } from "../../context/AuthContext";
import eventApi from "../../api/eventApi";
import { governanceApi } from "../../api/governanceApi";
import { useToast } from "../../components/ToastProvider";
import { formatDateRange, formatPrice } from "../../utils/format";
import "./MyConcertsPage.css";
import "../admin/AdminPages.css";

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
  const toast = useToast();
  const [events, setEvents] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [deletingId, setDeletingId] = useState(null);
  const [cancelModalEvent, setCancelModalEvent] = useState(null);
  const [cancelReason, setCancelReason] = useState("");
  const [cancelEvidenceFile, setCancelEvidenceFile] = useState(null);
  const [cancelSubmitting, setCancelSubmitting] = useState(false);

  useEffect(() => {
    setLoading(true);
    eventApi
      .getMine()
      .then((res) => setEvents(res.data?.data || []))
      .catch(() => setError("Could not load your events list."))
      .finally(() => setLoading(false));
  }, []);

  const openCancelModal = (event) => {
    event.preventDefault();
    event.stopPropagation();
    const eventId = Number(event.currentTarget.dataset.eventId);
    const title = event.currentTarget.dataset.eventTitle;
    setCancelModalEvent({ eventId, title });
    setCancelReason("");
    setCancelEvidenceFile(null);
    setError("");
  };

  const confirmCancelConcert = async () => {
    if (!cancelModalEvent) return;
    if (cancelReason.trim().length < 10) {
      setError("Cancellation reason must contain at least 10 characters.");
      return;
    }
    if (!cancelEvidenceFile) {
      setError("Official legal evidence / notarized document (PDF/Image) is required to cancel this concert.");
      return;
    }

    setCancelSubmitting(true);
    setError("");
    try {
      const formData = new FormData();
      formData.append("type", "Cancel");
      formData.append("reason", cancelReason.trim());
      formData.append("evidenceFile", cancelEvidenceFile);
      await governanceApi.submitChange(cancelModalEvent.eventId, formData);
      toast.success("Cancellation request and legal evidence submitted. Awaiting Platform Admin review.");
      setCancelModalEvent(null);
      const res = await eventApi.getMine();
      setEvents(res.data?.data || []);
    } catch (err) {
      setError(err.response?.data?.message || "Could not submit cancellation request.");
    } finally {
      setCancelSubmitting(false);
    }
  };

  const handleDeleteDraft = async (event) => {
    event.preventDefault();
    event.stopPropagation();
    const eventId = Number(event.currentTarget.dataset.eventId);
    const title = event.currentTarget.dataset.eventTitle;
    if (!window.confirm(`Delete event "${title}"? This action cannot be undone.`)) return;

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
  };

  return (
    <OrganizerShell title="My Concerts">
      <div className="tb-container mc-wrap">
        <div className="mc-head">
          <div>
            <h1>My Events</h1>
            <p className="mc-head-sub">
              Manage your concert submissions, ticket setups, and live on-site operations.
            </p>
          </div>
          {isOrganizer && (
            <Link to="/organizer/events/new" className="tb-btn tb-btn-primary mc-create-btn">
              + Create New Event
            </Link>
          )}
        </div>

        {loading && <div className="tb-loading">Loading your events...</div>}
        {!loading && error && <div className="tb-error">{error}</div>}
        {!loading && !error && events.length === 0 && (
          <div className="mc-empty-card">
            <div className="mc-empty-icon">🎪</div>
            <h3>No events created yet</h3>
            <p>Start organizing your first concert or live music event with ConcertShield.</p>
            <Link to="/organizer/events/new" className="tb-btn tb-btn-primary">
              + Create Your First Event
            </Link>
          </div>
        )}

        {!loading && events.length > 0 && (
          <div className="mc-list">
            {events.map((ev) => {
              const isDraftOrRejected = ["Draft", "Rejected"].includes(ev.status);
              const isPublished = ev.status === "Published";
              const isPending = ev.status === "Pending";

              return (
                <article key={ev.eventId} className="mc-card">
                  {/* Cột 1: Poster hình ảnh */}
                  <Link
                    to={`/organizer/events/${ev.eventId}/edit`}
                    className="mc-card-poster-link"
                    title={`Edit ${ev.title}`}
                  >
                    <div className="mc-card-poster">
                      {ev.posterUrl ? (
                        <img src={ev.posterUrl} alt={ev.title} />
                      ) : (
                        <div className="mc-card-poster-placeholder">
                          <span>No Poster</span>
                        </div>
                      )}
                    </div>
                  </Link>

                  {/* Cột 2: Thông tin sự kiện và Quick Chips */}
                  <div className="mc-card-info">
                    <div className="mc-card-title-row">
                      <Link
                        to={`/organizer/events/${ev.eventId}/edit`}
                        className="mc-card-title-link"
                      >
                        <h3 className="mc-card-title">{ev.title}</h3>
                      </Link>
                      <span className={"mc-status-pill " + (STATUS_CLASS[ev.status] || "")}>
                        {STATUS_LABELS[ev.status] || ev.status}
                      </span>
                    </div>

                    <div className="mc-card-meta-list">
                      <div className="mc-meta-item">
                        <span className="mc-meta-icon" aria-hidden="true">📅</span>
                        <span>{formatDateRange(ev.startsAt, ev.endsAt)}</span>
                      </div>
                      <div className="mc-meta-item">
                        <span className="mc-meta-icon" aria-hidden="true">📍</span>
                        <span>{ev.city || "Venue TBA"}</span>
                        <span className="mc-meta-bullet">•</span>
                        <span className="mc-meta-price">
                          {ev.minPrice != null ? `From ${formatPrice(ev.minPrice)}` : "Price TBA"}
                        </span>
                      </div>
                    </div>

                    {/* Thanh chip cấu hình nhanh */}
                    <div className="mc-config-chip-group">
                      <span className="mc-config-chip-label">Quick Settings:</span>
                      {isDraftOrRejected && (
                        <>
                          <Link to={`/organizer/events/${ev.eventId}/compliance`} className="mc-chip">
                            📋 Compliance
                          </Link>
                          <Link to={`/organizer/events/${ev.eventId}/changes`} className="mc-chip">
                            🕒 Schedule
                          </Link>
                          <Link to={`/organizer/events/${ev.eventId}/seating`} className="mc-chip">
                            🪑 Seating
                          </Link>
                          <Link to={`/organizer/events/${ev.eventId}/pricing`} className="mc-chip">
                            🏷️ Pricing
                          </Link>
                          <Link to={`/organizer/events/${ev.eventId}/refunds`} className="mc-chip">
                            🔄 Refunds
                          </Link>
                        </>
                      )}

                      {isPublished && (
                        <>
                          <Link to={`/organizer/events/${ev.eventId}/checkin`} className="mc-chip">
                            👥 Staff & Gates
                          </Link>
                          <Link to={`/organizer/events/${ev.eventId}/revenue`} className="mc-chip">
                            📊 Revenue
                          </Link>
                          <Link to={`/organizer/events/${ev.eventId}/seating`} className="mc-chip">
                            🪑 Seating
                          </Link>
                          <Link to={`/organizer/events/${ev.eventId}/pricing`} className="mc-chip">
                            🏷️ Pricing
                          </Link>
                          <Link to={`/organizer/events/${ev.eventId}/refunds`} className="mc-chip">
                            🔄 Refunds
                          </Link>
                        </>
                      )}

                      {isPending && (
                        <>
                          <Link to={`/organizer/events/${ev.eventId}/compliance`} className="mc-chip">
                            📋 Compliance
                          </Link>
                          <Link to={`/organizer/events/${ev.eventId}/seating`} className="mc-chip">
                            🪑 Seating
                          </Link>
                          <Link to={`/organizer/events/${ev.eventId}/pricing`} className="mc-chip">
                            🏷️ Pricing
                          </Link>
                          <Link to={`/organizer/events/${ev.eventId}/refunds`} className="mc-chip">
                            🔄 Refunds
                          </Link>
                        </>
                      )}
                    </div>
                  </div>

                  {/* Cột 3: Nút thao tác chính */}
                  <div className="mc-card-actions">
                    {isPublished && (
                      <Link
                        to={`/organizer/events/${ev.eventId}/realtime-checkin`}
                        className="mc-btn-live-checkin"
                        title="Open real-time check-in monitor"
                      >
                        📡 Live Check-in
                      </Link>
                    )}

                    <Link
                      to={`/organizer/events/${ev.eventId}/edit`}
                      className="mc-btn-edit"
                    >
                      {ev.status === "Cancelled" ? "👁️ View Details" : isPublished ? "✏️ View / Edit" : "✏️ Edit Event"}
                    </Link>

                    <Link
                      to={`/organizer/events/${ev.eventId}/orders`}
                      className="mc-btn-orders"
                    >
                      📦 View Orders
                    </Link>

                    {(isPublished || isPending) && (
                      <button
                        type="button"
                        className="mc-btn-delete"
                        style={{ color: "#ef4444", borderColor: "#fca5a5" }}
                        data-event-id={ev.eventId}
                        data-event-title={ev.title}
                        onClick={openCancelModal}
                        title={`Cancel this ${ev.status.toLowerCase()} concert`}
                      >
                        🚫 Cancel Concert
                      </button>
                    )}

                    {isDraftOrRejected && (
                      <button
                        type="button"
                        className="mc-btn-delete"
                        data-event-id={ev.eventId}
                        data-event-title={ev.title}
                        onClick={handleDeleteDraft}
                        disabled={deletingId === ev.eventId}
                        title={`Delete this ${ev.status.toLowerCase()} event`}
                      >
                        🗑️ {deletingId === ev.eventId ? "Deleting..." : `Delete ${ev.status.toLowerCase()}`}
                      </button>
                    )}
                  </div>
                </article>
              );
            })}
          </div>
        )}
        {cancelModalEvent && (
          <div
            className="admin-confirm-modal-overlay"
            onClick={() => !cancelSubmitting && setCancelModalEvent(null)}
          >
            <div
              className="admin-confirm-modal"
              onClick={(e) => e.stopPropagation()}
              style={{ maxWidth: "520px" }}
            >
              <div className="admin-confirm-modal-header">
                <div
                  className="admin-confirm-modal-icon"
                  style={{ background: "#fee2e2", color: "#dc2626" }}
                >
                  ⚠️
                </div>
                <div>
                  <h3
                    className="admin-confirm-modal-title"
                    style={{ color: "#991b1b" }}
                  >
                    Request Concert Cancellation
                  </h3>
                  <p className="admin-confirm-modal-body" style={{ margin: 0 }}>
                    Submit an official cancellation request for{" "}
                    <strong>"{cancelModalEvent.title}"</strong> for Administrator review.
                  </p>
                </div>
              </div>

              <div
                style={{
                  background: "#fef2f2",
                  border: "1px solid #fecaca",
                  borderRadius: "10px",
                  padding: "12px 14px",
                  margin: "16px 0",
                  fontSize: "13px",
                  color: "#991b1b",
                  lineHeight: 1.5,
                }}
              >
                <p style={{ margin: "0 0 6px 0", fontWeight: 700 }}>
                  Cancellation Review & Governance:
                </p>
                <ul style={{ margin: 0, paddingLeft: "18px" }}>
                  <li>
                    Your cancellation request and notarized legal proof will be reviewed by the Platform Administrator.
                  </li>
                  <li>
                    Once approved, ticket sales will be stopped permanently and all active tickets will enter the refund reconciliation queue.
                  </li>
                  <li>
                    Official cancellation and refund notices will be emailed to all ticket holders.
                  </li>
                </ul>
              </div>

              <div className="ow-field" style={{ margin: "16px 0" }}>
                <label
                  style={{
                    display: "block",
                    fontSize: "13px",
                    fontWeight: 600,
                    color: "#374151",
                    marginBottom: "6px",
                  }}
                >
                  Cancellation Reason * (minimum 10 characters)
                </label>
                <textarea
                  rows={3}
                  value={cancelReason}
                  onChange={(e) => setCancelReason(e.target.value)}
                  placeholder="Explain the official reason for cancellation (e.g. artist illness, severe weather, force majeure)..."
                  maxLength={1000}
                  style={{
                    width: "100%",
                    padding: "10px 12px",
                    border: "1px solid #d1d5db",
                    borderRadius: "8px",
                    boxSizing: "border-box",
                    fontSize: "14px",
                    resize: "vertical",
                  }}
                />
                <div
                  style={{
                    display: "flex",
                    justifyContent: "space-between",
                    fontSize: "12px",
                    color:
                      cancelReason.trim().length < 10 ? "#ef4444" : "#16a34a",
                    marginTop: "4px",
                  }}
                >
                  <span>
                    {cancelReason.trim().length < 10
                      ? `Need at least ${
                          10 - cancelReason.trim().length
                        } more characters`
                      : "✓ Valid reason"}
                  </span>
                  <span>{cancelReason.length} / 1000</span>
                </div>
              </div>

              {/* Bằng chứng pháp lý / Công văn có mộc đỏ hoặc công chứng */}
              <div className="ow-field" style={{ margin: "16px 0" }}>
                <label
                  style={{
                    display: "flex",
                    justifyContent: "space-between",
                    fontSize: "13px",
                    fontWeight: 600,
                    color: "#374151",
                    marginBottom: "6px",
                  }}
                >
                  <span>Supporting Legal Evidence / Notarized Proof *</span>
                  <span style={{ fontSize: "11px", color: "#6b7280" }}>PDF, PNG, JPG (Max 15MB)</span>
                </label>
                {!cancelEvidenceFile ? (
                  <label
                    style={{
                      display: "flex",
                      flexDirection: "column",
                      alignItems: "center",
                      justifyContent: "center",
                      padding: "18px 16px",
                      border: "2px dashed #d1d5db",
                      borderRadius: "10px",
                      background: "#f9fafb",
                      cursor: "pointer",
                      transition: "all 0.2s ease",
                      textAlign: "center",
                    }}
                    onMouseEnter={(e) => (e.currentTarget.style.borderColor = "#93c5fd")}
                    onMouseLeave={(e) => (e.currentTarget.style.borderColor = "#d1d5db")}
                  >
                    <input
                      type="file"
                      accept=".pdf,image/png,image/jpeg,image/jpg"
                      style={{ display: "none" }}
                      onChange={(e) => {
                        const file = e.target.files?.[0];
                        if (file) {
                          if (file.size > 15 * 1024 * 1024) {
                            toast.error("File exceeds maximum allowed size (15MB).");
                            return;
                          }
                          setCancelEvidenceFile(file);
                        }
                      }}
                    />
                    <div style={{ fontSize: "26px", marginBottom: "4px" }}>📑</div>
                    <div style={{ fontSize: "13px", fontWeight: 600, color: "#1f2937" }}>
                      Click to attach notarized notice / official decision
                    </div>
                    <div style={{ fontSize: "12px", color: "#6b7280", marginTop: "2px" }}>
                      Official authority notice, health certificate, or ministerial decree
                    </div>
                  </label>
                ) : (
                  <div
                    style={{
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "space-between",
                      padding: "10px 14px",
                      background: "#eff6ff",
                      border: "1px solid #bfdbfe",
                      borderRadius: "10px",
                    }}
                  >
                    <div style={{ display: "flex", alignItems: "center", gap: "10px", overflow: "hidden" }}>
                      <span
                        style={{
                          background: cancelEvidenceFile.name.toLowerCase().endsWith(".pdf") ? "#ef4444" : "#3b82f6",
                          color: "#fff",
                          fontSize: "11px",
                          fontWeight: 700,
                          padding: "3px 7px",
                          borderRadius: "5px",
                          textTransform: "uppercase",
                        }}
                      >
                        {cancelEvidenceFile.name.split(".").pop()}
                      </span>
                      <div style={{ overflow: "hidden" }}>
                        <div
                          style={{
                            fontSize: "13px",
                            fontWeight: 600,
                            color: "#1e3a8a",
                            whiteSpace: "nowrap",
                            overflow: "hidden",
                            textOverflow: "ellipsis",
                            maxWidth: "280px",
                          }}
                          title={cancelEvidenceFile.name}
                        >
                          {cancelEvidenceFile.name}
                        </div>
                        <div style={{ fontSize: "11px", color: "#2563eb" }}>
                          {(cancelEvidenceFile.size / (1024 * 1024)).toFixed(2)} MB • Ready for legal verification
                        </div>
                      </div>
                    </div>
                    <button
                      type="button"
                      style={{
                        background: "none",
                        border: "none",
                        color: "#dc2626",
                        fontSize: "12px",
                        fontWeight: 600,
                        cursor: "pointer",
                        padding: "4px 8px",
                      }}
                      onClick={() => setCancelEvidenceFile(null)}
                    >
                      ✕ Remove
                    </button>
                  </div>
                )}
                {!cancelEvidenceFile && (
                  <div style={{ fontSize: "12px", color: "#ef4444", marginTop: "4px" }}>
                    * Official notarized/stamped legal document is required to proceed.
                  </div>
                )}
              </div>

              <div className="admin-confirm-modal-actions">
                <button
                  type="button"
                  className="tb-btn tb-btn-outline ow-btn-sm"
                  onClick={() => setCancelModalEvent(null)}
                  disabled={cancelSubmitting}
                >
                  Keep Concert
                </button>
                <button
                  type="button"
                  className="tb-btn tb-btn-primary ow-btn-sm"
                  style={{
                    background: (!cancelEvidenceFile || cancelReason.trim().length < 10) ? "#9ca3af" : "#dc2626",
                    borderColor: (!cancelEvidenceFile || cancelReason.trim().length < 10) ? "#9ca3af" : "#dc2626"
                  }}
                  onClick={confirmCancelConcert}
                  disabled={
                    cancelSubmitting || cancelReason.trim().length < 10 || !cancelEvidenceFile
                  }
                >
                  {cancelSubmitting ? "Submitting Request..." : "Submit Cancellation Request to Admin"}
                </button>
              </div>
            </div>
          </div>
        )}
      </div>
    </OrganizerShell>
  );
}
