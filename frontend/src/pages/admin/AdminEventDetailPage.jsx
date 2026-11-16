import { useEffect, useState } from "react";
import { useParams, useNavigate, Link } from "react-router-dom";
import AdminShell from "./AdminShell";
import eventApi from "../../api/eventApi";
import { formatDateRange } from "../../utils/format";
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
            <div className="admin-result-icon">
              {result.type === "approved" ? "✅" : result.type === "warning" ? "⚠️" : "❌"}
            </div>
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
                    Previous rejection reason: {event.rejectedReason}
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
                  {showReject ? "Close" : "✕ Reject"}
                </button>
                <button
                  type="button"
                  className="tb-btn tb-btn-primary"
                  onClick={requestApprove}
                  disabled={submitting || !complianceReady}
                >
                  {submitting ? "Working..." : "✓ Approve & Publish"}
                </button>
              </div>
            )}

            {showApproveConfirm && (
              <div className="admin-confirm-modal-overlay" onClick={() => setShowApproveConfirm(false)}>
                <div className="admin-confirm-modal" onClick={(e) => e.stopPropagation()}>
                  <div className="admin-confirm-modal-header">
                    <div className="admin-confirm-modal-icon">🎉</div>
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
