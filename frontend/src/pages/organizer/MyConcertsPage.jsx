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

  const [deleteModalEvent, setDeleteModalEvent] = useState(null);

  const openDeleteModal = (event) => {
    event.preventDefault();
    event.stopPropagation();
    const eventId = Number(event.currentTarget.dataset.eventId);
    const title = event.currentTarget.dataset.eventTitle;
    const status = event.currentTarget.dataset.eventStatus || "Draft";
    setDeleteModalEvent({ eventId, title, status });
  };

  const confirmDeleteDraft = async () => {
    if (!deleteModalEvent) return;
    const { eventId, title } = deleteModalEvent;
    setDeletingId(eventId);
    setError("");
    try {
      await eventApi.remove(eventId);
      setEvents((items) => items.filter((item) => item.eventId !== eventId));
      toast.success(`Event "${title}" deleted.`);
      setDeleteModalEvent(null);
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

        {loading && <div className="tb-loading"><span className="app-loading-spinner" role="status" aria-label="Loading" /></div>}
        {!loading && error && <div className="tb-error">{error}</div>}
        {!loading && !error && events.length === 0 && (
          <div className="mc-empty-card">
            <h3>No events created yet</h3>
            <p>Start organizing your first concert or live music event with ConcertShield.</p>
            <Link to="/organizer/events/new" className="tb-btn tb-btn-primary">
              Create Your First Event
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
                  {/* Main section: Poster + Info + Primary CTAs */}
                  <div className="mc-card-main">
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
                          <span>{formatDateRange(ev.startsAt, ev.endsAt)}</span>
                        </div>
                        <div className="mc-meta-item">
                          <span>{ev.city || "Venue TBA"}</span>
                          <span className="mc-meta-bullet">•</span>
                          <span className="mc-meta-price">
                            {ev.minPrice != null ? `From ${formatPrice(ev.minPrice)}` : "Price TBA"}
                          </span>
                        </div>
                      </div>
                    </div>

                    {/* Primary CTAs in Top Right (Cleanly separated by event lifecycle) */}
                    <div className="mc-card-primary-actions">
                      {isPublished && (
                        <Link
                          to={`/organizer/events/${ev.eventId}/realtime-checkin`}
                          className="mc-btn-live-checkin"
                          title="Open real-time check-in monitor"
                        >
                          Live Check-in
                        </Link>
                      )}

                      {isPublished ? (
                        <>
                          <Link
                            to={`/organizer/events/${ev.eventId}/changes`}
                            className="mc-btn-edit"
                            title="Submit Event Change Request - Reschedule, Postpone, or Cancel"
                          >
                            Submit Change Request
                          </Link>
                          <Link
                            to={`/organizer/events/${ev.eventId}/edit`}
                            className="mc-chip"
                            style={{ padding: "5px 12px", textAlign: "center" }}
                            title="View event configuration in read-only mode"
                          >
                            View Details
                          </Link>
                        </>
                      ) : (
                        <Link
                          to={`/organizer/events/${ev.eventId}/edit`}
                          className="mc-btn-edit"
                        >
                          {ev.status === "Cancelled"
                            ? "View Details"
                            : isPending
                            ? "View Details"
                            : "Edit Event"}
                        </Link>
                      )}
                    </div>
                  </div>

                  {/* Card Footer: Quick Settings on left, Delete Draft for drafts */}
                  <div className="mc-card-footer">
                    <div className="mc-footer-left">
                      <span className="mc-config-chip-label">Quick Settings:</span>
                      <Link to={`/organizer/events/${ev.eventId}/orders`} className="mc-chip">
                        Orders
                      </Link>
                      {isPublished && (
                        <>
                          <Link to={`/organizer/events/${ev.eventId}/checkin`} className="mc-chip">
                            Staff & Gates
                          </Link>
                          <Link to={`/organizer/events/${ev.eventId}/revenue`} className="mc-chip">
                            Revenue
                          </Link>
                        </>
                      )}
                      {(isDraftOrRejected || isPending) && (
                        <Link to={`/organizer/events/${ev.eventId}/compliance`} className="mc-chip">
                          Compliance
                        </Link>
                      )}
                      <Link to={`/organizer/events/${ev.eventId}/seating`} className="mc-chip">
                        Seating
                      </Link>
                      <Link to={`/organizer/events/${ev.eventId}/pricing`} className="mc-chip">
                        Pricing
                      </Link>
                      <Link to={`/organizer/events/${ev.eventId}/refunds`} className="mc-chip">
                        Refunds
                      </Link>
                    </div>

                    <div className="mc-footer-right">
                      {isDraftOrRejected && (
                        <button
                          type="button"
                          className="mc-footer-action-btn mc-footer-action-danger"
                          data-event-id={ev.eventId}
                          data-event-title={ev.title}
                          data-event-status={ev.status}
                          onClick={openDeleteModal}
                          disabled={deletingId === ev.eventId}
                          title={`Delete this ${ev.status.toLowerCase()} event`}
                        >
                          {deletingId === ev.eventId ? <span className="app-loading-spinner" role="status" aria-label="Loading" /> : "Delete Draft"}
                        </button>
                      )}
                    </div>
                  </div>
                </article>
              );
            })}
          </div>
        )}
        {deleteModalEvent && (
          <div
            className="admin-confirm-modal-overlay"
            onClick={() => !deletingId && setDeleteModalEvent(null)}
          >
            <div
              className="admin-confirm-modal"
              onClick={(e) => e.stopPropagation()}
              style={{ maxWidth: "460px" }}
            >
              <div className="admin-confirm-modal-header">
                <div>
                  <h3
                    className="admin-confirm-modal-title"
                    style={{ color: "#991b1b" }}
                  >
                    Delete Event Draft
                  </h3>
                  <p className="admin-confirm-modal-body" style={{ margin: "8px 0 0", color: "#374151" }}>
                    Are you sure you want to permanently delete <strong>"{deleteModalEvent.title}"</strong>? This action cannot be undone.
                  </p>
                </div>
              </div>

              <div className="admin-confirm-modal-actions" style={{ marginTop: "20px" }}>
                <button
                  type="button"
                  className="tb-btn tb-btn-outline ow-btn-sm"
                  onClick={() => setDeleteModalEvent(null)}
                  disabled={!!deletingId}
                >
                  No, Keep It
                </button>
                <button
                  type="button"
                  className="tb-btn tb-btn-primary ow-btn-sm"
                  style={{ background: "#dc2626", borderColor: "#dc2626", color: "#ffffff" }}
                  onClick={confirmDeleteDraft}
                  disabled={!!deletingId}
                >
                  {deletingId ? <span className="app-loading-spinner" role="status" aria-label="Loading" /> : "Yes, Delete Event"}
                </button>
              </div>
            </div>
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
                <div>
                  <h3
                    className="admin-confirm-modal-title"
                    style={{ color: "#991b1b" }}
                  >
                    Concert Cancellation Request
                  </h3>
                  <p className="admin-confirm-modal-body" style={{ margin: "4px 0 0" }}>
                    Submit an official cancellation request for <strong>"{cancelModalEvent.title}"</strong> to Platform Admin.
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
                  Cancellation Process & Risk Governance:
                </p>
                <ul style={{ margin: 0, paddingLeft: "18px" }}>
                  <li>Your cancellation request and legal evidence documents will be audited by Platform Administration.</li>
                  <li>Once approved, ticket sales terminate permanently and paid tickets transition into the automated refund reconciliation cycle.</li>
                  <li>Official notifications will be dispatched automatically to all ticket buyers.</li>
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
                  Reason for cancellation * (min 10 characters)
                </label>
                <textarea
                  rows={3}
                  value={cancelReason}
                  onChange={(e) => setCancelReason(e.target.value)}
                  placeholder="Provide detailed explanation (e.g. force majeure, artist medical emergency, venue closure, extreme weather)..."
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
                      ? `At least ${
                          10 - cancelReason.trim().length
                        } more characters required`
                      : "Reason meets requirements"}
                  </span>
                  <span>{cancelReason.length} / 1000</span>
                </div>
              </div>

              {/* Official Legal Evidence Upload */}
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
                  <span>Legal Evidence Document *</span>
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
                    <div style={{ fontSize: "13px", fontWeight: 600, color: "#1f2937" }}>
                      Click to upload official decree / cancellation notice
                    </div>
                    <div style={{ fontSize: "12px", color: "#6b7280", marginTop: "2px" }}>
                      Government decree, venue contract termination notice, or force majeure certification
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
                      Remove
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
                  {cancelSubmitting ? <span className="app-loading-spinner" role="status" aria-label="Loading" /> : "Submit Cancellation Request to Admin"}
                </button>
              </div>
            </div>
          </div>
        )}
      </div>
    </OrganizerShell>
  );
}
