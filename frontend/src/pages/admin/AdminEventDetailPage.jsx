import { useEffect, useState } from "react";
import { useParams, useNavigate, Link } from "react-router-dom";
import AdminShell from "./AdminShell";
import eventApi from "../../api/eventApi";
import { formatDateRange } from "../../utils/format";
import "../organizer/OrganizerWizard.css";
import "./AdminPages.css";
import GovernancePanel from "../../components/GovernancePanel";
import StepTicketsSeating from "../organizer/steps/StepTicketsSeating";

export default function AdminEventDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();

  const [event, setEvent] = useState(null);
  const [complianceReady, setComplianceReady] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [errorDetails, setErrorDetails] = useState([]);

  const [showReject, setShowReject] = useState(false);
  const [reason, setReason] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [result, setResult] = useState(null); // { type: "approved"|"rejected", message }

  const load = () => {
    setLoading(true);
    setError("");
    setErrorDetails([]);
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

  const handleApprove = async () => {
    if (!complianceReady) { setError("Review and approve the current compliance documents first."); return; }
    if (!window.confirm("Approve this event and publish it?")) return;
    setSubmitting(true);
    setError("");
    try {
      const res = await eventApi.approve(id);
      setEvent(res.data?.data || event);
      setResult({ type: "approved", message: res.data?.message });
    } catch (err) {
      const apiErrors = err.response?.data?.errors || err.response?.data?.data?.errors || [];
      setError(err.response?.data?.message || "Could not approve this event.");
      setErrorDetails(Array.isArray(apiErrors) ? apiErrors : []);
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
      setResult({ type: "rejected", message: res.data?.message });
      setShowReject(false);
    } catch (err) {
      setError(err.response?.data?.message || "Could not reject this event.");
    } finally {
      setSubmitting(false);
    }
  };

  const [toggleLoading, setToggleLoading] = useState(false);

  const handleToggleFeatured = async () => {
    setToggleLoading(true);
    setError("");
    try {
      const res = await eventApi.toggleFeatured(id);
      const updated = res.data?.data;
      if (updated) {
        setEvent(updated);
      } else {
        setEvent((prev) => ({ ...prev, isFeatured: !prev.isFeatured }));
      }
      setResult({
        type: "info",
        message: res.data?.message || (event.isFeatured ? "Removed from Featured" : "Marked as Featured")
      });
    } catch (err) {
      setError(err.response?.data?.message || "Could not update Featured status.");
    } finally {
      setToggleLoading(false);
    }
  };

  const canDecide = event?.status === "Pending";

  return (
    <AdminShell title="Review Event">
      <div className="ow-wrap">
        <div className="ow-head">
          <div>
            <h1>Review Event</h1>
            <p className="ow-sub">
              Check everything below, then approve or reject this submission.
            </p>
          </div>
          <Link to="/admin/events" className="tb-btn tb-btn-outline" style={{ textDecoration: "none" }}>
            ← Back to queue
          </Link>
        </div>

        {error && (
          <div className="ow-error">
            <div><strong>{error}</strong></div>
            {errorDetails.length > 0 && (
              <ul style={{ margin: "8px 0 0", paddingLeft: "20px" }}>
                {errorDetails.map((msg, idx) => (
                  <li key={idx}>{msg}</li>
                ))}
              </ul>
            )}
          </div>
        )}
        {loading && <div className="tb-loading">Loading...</div>}

        {result && (
          <div
            className={
              "ow-banner " +
              (result.type === "approved"
                ? "ow-banner-published"
                : "ow-banner-rejected")
            }
          >
            <strong>
              {result.type === "approved" ? "Approved." : "Rejected."}
            </strong>{" "}
            {result.message}
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
                <div style={{ display: "flex", alignItems: "center", gap: "8px", margin: "6px 0 10px", flexWrap: "wrap" }}>
                  <span
                    className={
                      "admin-status-pill admin-status-" + (event.status || "").toLowerCase()
                    }
                  >
                    {event.status}
                  </span>
                  {event.isFeatured ? (
                    <span style={{
                      background: "rgba(234, 179, 8, 0.15)",
                      border: "1px solid rgba(234, 179, 8, 0.4)",
                      color: "#facc15",
                      padding: "2px 8px",
                      borderRadius: "999px",
                      fontSize: "12px",
                      fontWeight: 600
                    }}>
                      ⭐ Featured on Homepage
                    </span>
                  ) : (
                    <span style={{
                      background: "rgba(148, 163, 184, 0.1)",
                      border: "1px solid rgba(148, 163, 184, 0.2)",
                      color: "#94a3b8",
                      padding: "2px 8px",
                      borderRadius: "999px",
                      fontSize: "12px"
                    }}>
                      Regular Event
                    </span>
                  )}
                  <button
                    type="button"
                    className="tb-btn tb-btn-outline"
                    onClick={handleToggleFeatured}
                    disabled={toggleLoading}
                    style={{
                      padding: "4px 10px",
                      fontSize: "12px",
                      borderRadius: "6px",
                      borderColor: event.isFeatured ? "#eab308" : "rgba(255, 255, 255, 0.2)",
                      color: event.isFeatured ? "#fef08a" : "#cbd5e1"
                    }}
                  >
                    {toggleLoading
                      ? "Updating..."
                      : event.isFeatured
                      ? "⭐ Unmark Featured"
                      : "☆ Mark as Featured"}
                  </button>
                </div>
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
                {event.bannerUrl && <img className="admin-event-banner" src={event.bannerUrl} alt="Event banner" />}
                <div className="admin-event-gallery">
                  {(event.images || []).map((image) => <img key={image.imageId} src={image.imageUrl} alt="Organizer upload" />)}
                </div>
              </section>
            )}

            <section className="ow-section admin-seating-editor">
              <h3>Seating Chart, Tickets &amp; Pricing</h3>
              <p className="ow-hint">
                Full editor — build/edit the seating chart on the canvas,
                apply or save templates, and configure ticket classes,
                dynamic pricing and refund policy, the same tools the
                organizer uses. As Admin you can adjust these regardless of
                the event's current status.
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
                className="tb-btn tb-btn-outline"
                onClick={handleToggleFeatured}
                disabled={toggleLoading}
                style={{
                  borderColor: event.isFeatured ? "#eab308" : "rgba(255, 255, 255, 0.25)",
                  color: event.isFeatured ? "#fef08a" : "inherit"
                }}
              >
                {toggleLoading
                  ? "Updating..."
                  : event.isFeatured
                  ? "⭐ Remove from Featured"
                  : "⭐ Mark as Featured"}
              </button>
              {canDecide && (
                <>
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
                    onClick={handleApprove}
                    disabled={submitting || !complianceReady}
                  >
                    {submitting ? "Working..." : "✓ Approve & Publish"}
                  </button>
                </>
              )}
            </div>

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
