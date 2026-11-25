import { useEffect, useState } from "react";
import { useParams, useNavigate, Link } from "react-router-dom";
import AdminShell from "./AdminShell";
import eventApi from "../../api/eventApi";
import { formatDateRange, cleanReason } from "../../utils/format";
import { useToast } from "../../components/ToastProvider";
import "../organizer/OrganizerWizard.css";
import "./AdminPages.css";
import GovernancePanel from "../../components/GovernancePanel";
import StepTicketsSeating from "../organizer/steps/StepTicketsSeating";

export default function AdminEventDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const toast = useToast();

  const [event, setEvent] = useState(null);
  const [complianceReady, setComplianceReady] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const [showReject, setShowReject] = useState(false);
  const [showApproveConfirm, setShowApproveConfirm] = useState(false);
  const [reason, setReason] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [result, setResult] = useState(null); // { type: "approved"|"rejected", message }

  const load = () => {
    setLoading(true);
    setError("");
    eventApi
      .adminGetById(id)
      .then((res) => setEvent(res.data?.data || null))
      .catch((err) =>
        setError(err.response?.data?.message || "Could not load this event.")
      )
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  const requestApprove = () => {
    if (!complianceReady) { setError("Review and approve the current compliance documents first."); return; }
    setError("");
    setShowApproveConfirm(true);
  };

  const handleApprove = async () => {
    setShowApproveConfirm(false);
    setSubmitting(true);
    setError("");
    try {
      const res = await eventApi.approve(id);
      const updated = res.data?.data || event;
      setEvent(updated);
      const rawMsg = res.data?.message || "";
      const isRoleWarn = rawMsg.includes("Organizer role could not be granted");
      const cleanMsg = isRoleWarn
        ? "Concert approved and published successfully. (Note: Owner already has or is pending Organizer verification)."
        : (rawMsg || "Concert approved and published successfully.");

      setResult({
        type: isRoleWarn ? "warning" : "approved",
        title: "Event Approved & Published",
        message: cleanMsg
      });
      toast.success("Event approved and published successfully!");
    } catch (err) {
      setError(err.response?.data?.message || "Could not approve this event.");
    } finally {
      setSubmitting(false);
    }
  };

  const handleReject = async (e) => {
    e.preventDefault();
    if (!reason.trim()) {
      setError("A rejection reason is required.");
      return;
    }
    setSubmitting(true);
    setError("");
    try {
      const res = await eventApi.reject(id, reason.trim());
      setEvent(res.data?.data || event);
      setResult({
        type: "rejected",
        title: "Event Rejected",
        message: res.data?.message || "Event has been rejected and returned to Draft."
      });
      setShowReject(false);
      toast.info("Event rejection sent to organizer.");
    } catch (err) {
      setError(err.response?.data?.message || "Could not reject this event.");
    } finally {
      setSubmitting(false);
    }
  };

  const [showCancelModal, setShowCancelModal] = useState(false);
  const [cancelReason, setCancelReason] = useState("");
  const [cancelEvidenceFile, setCancelEvidenceFile] = useState(null);

  const handleConfirmCancelEvent = async () => {
    if (cancelReason.trim().length < 10) {
      setError("Cancellation reason must contain at least 10 characters.");
      return;
    }
    if (!cancelEvidenceFile) {
      setError("Official legal evidence or notarized document (PDF/Image) is required to cancel this event.");
      return;
    }
    setSubmitting(true);
    setError("");
    try {
      const formData = new FormData();
      formData.append("reason", cancelReason.trim());
      formData.append("evidenceFile", cancelEvidenceFile);
      const res = await eventApi.cancel(id, formData);
      setEvent(res.data?.data || { ...event, status: "Cancelled" });
      setResult({
        type: "cancelled",
        title: "Concert Cancelled",
        message: "The concert has been cancelled. Ticket sales are closed."
      });
      setShowCancelModal(false);
      toast.info("Concert has been cancelled.");
    } catch (err) {
      setError(err.response?.data?.message || "Could not cancel this event.");
    } finally {
      setSubmitting(false);
    }
  };

  const isPending = event?.status === "Pending";

  const pageTitle = isPending
    ? "Review Event"
    : `Event Details (${event?.status || ""})`;

  const pageSubtitle = isPending
    ? "Check everything below, then approve or reject this submission."
    : "Review the event overview, seating chart, and compliance information.";

  const activeSubnav = isPending ? "/admin/events" : "/admin/events/all";

  const canDecide = isPending;

  return (
    <AdminShell title={pageTitle} activeSubnavPath={activeSubnav}>
      <div className="ow-wrap">
        <div className="ow-head">
          <div>
            <h1>{pageTitle}</h1>
            <p className="ow-sub">{pageSubtitle}</p>
          </div>
          <Link
            to={isPending ? "/admin/events" : "/admin/events/all"}
            className="tb-btn tb-btn-outline"
            style={{ textDecoration: "none" }}
          >
            {isPending ? "← Back to queue" : "← Back to all events"}
          </Link>
        </div>

        {error && <div className="ow-error">{error}</div>}
        {loading && <div className="tb-loading">Loading...</div>}

        {result && (
          <div className={`admin-result-banner is-${result.type === "approved" ? "success" : result.type === "warning" ? "warning" : "danger"}`}>
            <div className="admin-result-content">
              <strong>{result.title}</strong>
              <span>{result.message}</span>
            </div>
          </div>
        )}

        {!loading && !error && event && (
          <div className="ow-review-summary">
            <div className="ow-review-row">
              {event.posterUrl && (
                <img className="ow-review-poster" src={event.posterUrl} alt="" />
              )}
              <div>
                <h3>{event.title}</h3>
                <span
                  className={
                    "admin-status-pill admin-status-" + (event.status || "").toLowerCase()
                  }
                >
                  {event.status}
                </span>
                {event.shortDescription && <p>{event.shortDescription}</p>}
                <p className="ow-hint">
                  {formatDateRange(event.startsAt, event.endsAt)}
                </p>
                <p className="ow-hint">
                  {[event.locationName, event.address, event.city]
                    .filter(Boolean)
                    .join(", ") || "No venue provided"}
                </p>
                {event.rejectedReason && (
                  <p className="ow-hint">
                    Previous rejection / cancellation reason: {cleanReason(event.rejectedReason)}
                  </p>
                )}
              </div>
            </div>

            {event.description && (
              <section className="ow-section">
                <h3>Description</h3>
                <p>{event.description}</p>
              </section>
            )}

            {(event.bannerUrl || event.images?.length > 0) && (
              <section className="ow-section">
                <h3>Organizer Images</h3>
                {event.bannerUrl && (
                  <img
                    className="admin-event-banner"
                    src={event.bannerUrl}
                    alt=""
                    onError={(e) => { e.currentTarget.style.display = 'none'; }}
                  />
                )}
                <div className="admin-event-gallery">
                  {(event.images || []).map((image) => (
                    <img
                      key={image.imageId}
                      src={image.imageUrl}
                      alt=""
                      onError={(e) => { e.currentTarget.style.display = 'none'; }}
                    />
                  ))}
                </div>
              </section>
            )}

            <section className="ow-section admin-event-policies">
              <h3>Admission, Seat Hold &amp; Re-entry Policies</h3>
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '16px', marginTop: '12px' }}>
                <div style={{ background: '#f8fafc', padding: '12px 16px', borderRadius: '8px', border: '1px solid #e2e8f0' }}>
                  <div style={{ fontSize: '12px', color: '#64748b', fontWeight: 600, textTransform: 'uppercase' }}>Seat Hold Timeout</div>
                  <div style={{ fontSize: '16px', fontWeight: 700, color: '#0f172a', marginTop: '4px' }}>
                    {event.holdTimeoutMinutes || 10} minutes
                  </div>
                  <div style={{ fontSize: '12px', color: '#64748b', marginTop: '4px' }}>
                    Seats held during selection before auto-release
                  </div>
                </div>

                <div style={{ background: '#f8fafc', padding: '12px 16px', borderRadius: '8px', border: '1px solid #e2e8f0' }}>
                  <div style={{ fontSize: '12px', color: '#64748b', fontWeight: 600, textTransform: 'uppercase' }}>Re-entry Policy</div>
                  <div style={{ fontSize: '16px', fontWeight: 700, color: '#0f172a', marginTop: '4px' }}>
                    {event.reEntryPolicy === 'TimeLimited' ? `Time-limited (${event.reEntryDurationMinutes || 30} mins)` : event.reEntryPolicy === 'Unlimited' ? 'Unlimited Re-entry' : 'No Re-entry'}
                  </div>
                  <div style={{ fontSize: '12px', color: '#64748b', marginTop: '4px' }}>
                    {event.reEntryPolicy === 'TimeLimited' ? `Attendee can exit and return within ${event.reEntryDurationMinutes || 30} mins` : event.reEntryPolicy === 'Unlimited' ? 'Attendee can exit and re-enter freely' : 'Once scanned in, attendee cannot re-enter if they exit'}
                  </div>
                </div>

                <div style={{ background: '#f8fafc', padding: '12px 16px', borderRadius: '8px', border: '1px solid #e2e8f0' }}>
                  <div style={{ fontSize: '12px', color: '#64748b', fontWeight: 600, textTransform: 'uppercase' }}>Purchase Limit</div>
                  <div style={{ fontSize: '16px', fontWeight: 700, color: '#0f172a', marginTop: '4px' }}>
                    Max {event.maxTicketsPerAccount || 4} tickets / account
                  </div>
                  <div style={{ fontSize: '12px', color: '#64748b', marginTop: '4px' }}>
                    Anti-scalping quota per customer account
                  </div>
                </div>
              </div>
            </section>

            <section className="ow-section admin-seating-editor">
              <h3>Seating Chart, Tickets &amp; Pricing</h3>
              <p className="ow-hint">
                Review the seating chart, ticket classes, dynamic pricing and refund policy configured by the organizer.
              </p>
              <StepTicketsSeating
                eventId={Number(id)}
                event={event}
                readOnly
                standalone
                onRefresh={load}
                onSaved={load}
              />
            </section>

            <GovernancePanel event={event} admin onUpdated={load} onCompliance={setComplianceReady} />
            {(event?.status === "Published" || event?.status === "Postponed") && (
              <div className="ow-actions" style={{ marginTop: '20px' }}>
                <button
                  type="button"
                  style={{
                    backgroundColor: "#dc2626",
                    color: "#ffffff",
                    border: "none",
                    borderRadius: "8px",
                    padding: "9px 18px",
                    fontWeight: 600,
                    fontSize: "14px",
                    cursor: "pointer"
                  }}
                  onClick={() => { setShowCancelModal(true); setCancelReason(""); setError(""); }}
                  disabled={submitting}
                >
                  Cancel Concert
                </button>
              </div>
            )}

            {showCancelModal && (
              <div
                className="admin-confirm-modal-overlay"
                onClick={() => !submitting && setShowCancelModal(false)}
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
                        Cancel Concert Confirmation
                      </h3>
                      <p className="admin-confirm-modal-body" style={{ margin: 0 }}>
                        Are you sure you want to cancel <strong>"{event.title}"</strong>?
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
                      Administrative Cancellation Consequences:
                    </p>
                    <ul style={{ margin: 0, paddingLeft: "18px" }}>
                      <li>Ticket sales will be closed permanently.</li>
                      <li>
                        All active tickets will be revoked and queued for full refund.
                      </li>
                      <li>
                        Official cancellation notice will be emailed to all attendees.
                      </li>
                      <li>This action is permanent and cannot be undone.</li>
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
                      placeholder="Explain the official reason for administrative cancellation..."
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
                          : "Valid reason"}
                      </span>
                      <span>{cancelReason.length} / 1000</span>
                    </div>
                  </div>

                  {/* Legal evidence / official notice */}
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
                        <div style={{ fontSize: "13px", fontWeight: 600, color: "#1f2937", marginTop: "4px" }}>
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
                      onClick={() => setShowCancelModal(false)}
                      disabled={submitting}
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
                      onClick={handleConfirmCancelEvent}
                      disabled={
                        submitting || cancelReason.trim().length < 10 || !cancelEvidenceFile
                      }
                    >
                      {submitting ? "Cancelling..." : "Confirm Cancellation"}
                    </button>
                  </div>
                </div>
              </div>
            )}
            {canDecide && (
              <div className="ow-actions">
                <button
                  type="button"
                  className="tb-btn tb-btn-outline"
                  onClick={() => navigate("/admin/events")}
                >
                  ← Back to queue
                </button>
                <button
                  type="button"
                  className="tb-btn tb-btn-outline admin-btn-danger"
                  onClick={() => setShowReject((v) => !v)}
                  disabled={submitting}
                >
                  {showReject ? "Close" : "Reject"}
                </button>
                <button
                  type="button"
                  className="tb-btn tb-btn-primary"
                  onClick={requestApprove}
                  disabled={submitting || !complianceReady}
                >
                  {submitting ? "Working..." : "Approve & Publish"}
                </button>
              </div>
            )}

            {showApproveConfirm && (
              <div className="admin-confirm-modal-overlay" onClick={() => setShowApproveConfirm(false)}>
                <div className="admin-confirm-modal" onClick={(e) => e.stopPropagation()}>
                  <div className="admin-confirm-modal-header">
                    <div>
                      <h3 className="admin-confirm-modal-title">Publish Event Confirmation</h3>
                      <p className="admin-confirm-modal-body" style={{ margin: 0 }}>
                        Are you sure you want to approve and publish <strong>"{event.title}"</strong>?
                      </p>
                    </div>
                  </div>
                  <p className="admin-confirm-modal-body">
                    Once published, tickets will become available to the public according to your configured sales schedule. Base ticket price, total quantity, and seating chart will be locked.
                  </p>
                  <div className="admin-confirm-modal-actions">
                    <button
                      type="button"
                      className="tb-btn tb-btn-outline ow-btn-sm"
                      onClick={() => setShowApproveConfirm(false)}
                      disabled={submitting}
                    >
                      Cancel
                    </button>
                    <button
                      type="button"
                      className="tb-btn tb-btn-primary ow-btn-sm"
                      onClick={handleApprove}
                      disabled={submitting}
                    >
                      {submitting ? "Publishing..." : "Yes, Approve & Publish"}
                    </button>
                  </div>
                </div>
              </div>
            )}

            {canDecide && showReject && (
              <form className="ow-inline-form" onSubmit={handleReject}>
                <label className="ow-field ow-span-2">
                  <span>Rejection reason *</span>
                  <textarea
                    value={reason}
                    onChange={(e) => setReason(e.target.value)}
                    rows={3}
                    maxLength={500}
                    placeholder="Explain what the organizer needs to fix before resubmitting."
                  />
                </label>
                <button
                  type="submit"
                  className="tb-btn tb-btn-primary ow-btn-sm admin-btn-danger"
                  disabled={submitting}
                >
                  {submitting ? "Sending..." : "Confirm rejection"}
                </button>
              </form>
            )}
          </div>
        )}
      </div>
    </AdminShell>
  );
}
