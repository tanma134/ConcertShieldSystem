import { useEffect, useState } from "react";
import { useParams, useNavigate, Link } from "react-router-dom";
import Header from "../../components/Header";
import Footer from "../../components/Footer";
import eventApi from "../../api/eventApi";
import { formatDateRange, formatPrice } from "../../utils/format";
import "../organizer/OrganizerWizard.css";
import "./AdminPages.css";

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
    <div className="tb-app">
      <Header />

      <div className="tb-container ow-wrap">
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

            {event.ticketTypes?.length > 0 && (
              <section className="ow-section">
                <h3>Ticket Types</h3>
                <table className="ow-table">
                  <thead>
                    <tr>
                      <th>Name</th>
                      <th>Price</th>
                      <th>Quantity</th>
                      <th>Per-order limit</th>
                    </tr>
                  </thead>
                  <tbody>
                    {event.ticketTypes.map((t) => (
                      <tr key={t.ticketTypeId}>
                        <td>
                          <div className="ow-td-title">{t.typeName}</div>
                          {t.description && (
                            <div className="ow-td-sub">{t.description}</div>
                          )}
                        </td>
                        <td>{formatPrice(t.price)}</td>
                        <td>{t.quantity}</td>
                        <td>
                          {t.minPerOrder}–{t.maxPerOrder}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </section>
            )}

            {event.seatingChart?.zones?.length > 0 && (
              <section className="ow-section">
                <h3>Seating Chart</h3>
                <table className="ow-table">
                  <thead>
                    <tr>
                      <th>Zone</th>
                      <th>Type</th>
                      <th>Capacity</th>
                      <th>Available</th>
                    </tr>
                  </thead>
                  <tbody>
                    {event.seatingChart.zones.map((z) => (
                      <tr key={z.seatZoneId}>
                        <td>{z.zoneName}</td>
                        <td>{z.zoneType === "Seated" ? "Seated" : "Standing"}</td>
                        <td>{z.capacity}</td>
                        <td>{z.availableSeats}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </section>
            )}

            {event.refundPolicies?.length > 0 && (
              <section className="ow-section">
                <h3>Refund Policies</h3>
                <table className="ow-table">
                  <thead>
                    <tr>
                      <th>Policy</th>
                      <th>Refund</th>
                      <th>Deadline</th>
                    </tr>
                  </thead>
                  <tbody>
                    {event.refundPolicies.map((p) => (
                      <tr key={p.refundPolicyId}>
                        <td>
                          <div className="ow-td-title">{p.policyName}</div>
                          {p.description && (
                            <div className="ow-td-sub">{p.description}</div>
                          )}
                        </td>
                        <td>{p.refundPercent}%</td>
                        <td>{p.deadlineBeforeEventHours} hours before</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </section>
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

      <Footer />
    </div>
  );
}
