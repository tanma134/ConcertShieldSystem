import { useEffect, useState } from "react";
import { useParams, useNavigate, Link } from "react-router-dom";
import AdminShell from "./AdminShell";
import eventApi from "../../api/eventApi";
import { formatDateRange } from "../../utils/format";
import "../organizer/OrganizerWizard.css";
import "./AdminPages.css";
import StepTicketsSeating from "../organizer/steps/StepTicketsSeating";

export default function AdminEventDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();

  const [event, setEvent] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const [showReject, setShowReject] = useState(false);
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

  const handleApprove = async () => {
    if (!window.confirm("Approve this event and publish it?")) return;
    setSubmitting(true);
    setError("");
    try {
      const res = await eventApi.approve(id);
      setEvent(res.data?.data || event);
      setResult({ type: "approved", message: res.data?.message });
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
      setResult({ type: "rejected", message: res.data?.message });
      setShowReject(false);
    } catch (err) {
      setError(err.response?.data?.message || "Could not reject this event.");
    } finally {
      setSubmitting(false);
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
          <Link to="/admin/events" className="tb-btn tb-btn-outline">
            ← Back to queue
          </Link>
        </div>

        {error && <div className="ow-error">{error}</div>}
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
                forceEditable
                standalone
                onRefresh={load}
                onSaved={load}
              />
            </section>

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
                  onClick={handleApprove}
                  disabled={submitting}
                >
                  {submitting ? "Working..." : "✓ Approve & Publish"}
                </button>
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
