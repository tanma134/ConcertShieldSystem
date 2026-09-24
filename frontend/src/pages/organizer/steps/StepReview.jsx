import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import eventApi from "../../../api/eventApi";
import { formatDateRange, formatPrice } from "../../../utils/format";

export default function StepReview({ eventId, event, onRefresh, onSaved, onBack, onSubmitted }) {
  const [validation, setValidation] = useState(null);
  const [checking, setChecking] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");
  const [submitted, setSubmitted] = useState(false);

  const canSubmit = event && ["Draft", "Rejected"].includes(event.status);

  const runValidation = async () => {
    setChecking(true);
    setError("");
    try {
      const res = await eventApi.validateForSubmission(eventId);
      setValidation(res.data?.data || null);
    } catch (err) {
      setError(err.response?.data?.message || "Could not check submission requirements.");
    } finally {
      setChecking(false);
    }
  };

  useEffect(() => {
    if (canSubmit) runValidation();
    else setChecking(false);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [eventId, event?.status]);

  const handleSaveDraft = async () => {
    await onRefresh();
    onSaved();
  };

  const handleSubmit = async () => {
    setSubmitting(true);
    setError("");
    try {
      await eventApi.submit(eventId);
      setSubmitted(true);
      await onSubmitted();
    } catch (err) {
      const apiErrors = err.response?.data?.errors;
      setError(
        (apiErrors && apiErrors.length > 0 && apiErrors.join(" • ")) ||
          err.response?.data?.message ||
          "Submission failed."
      );
      // The 400 body also carries a fresh validation result - use it if present.
      const data = err.response?.data?.data;
      if (data && "errors" in data) setValidation(data);
    } finally {
      setSubmitting(false);
    }
  };

  if (submitted || event?.status === "Pending") {
    return (
      <div className="ow-step-body">
        <h2>4. Review & Submit</h2>
        <div className="ow-banner ow-banner-pending">
          <strong>Submitted for review.</strong> This event is awaiting admin
          review. You'll be notified once there's a decision.
        </div>
        <Link to="/organizer/events" className="tb-btn tb-btn-primary">
          Back to my events
        </Link>
      </div>
    );
  }

  if (event?.status === "Published") {
    return (
      <div className="ow-step-body">
        <h2>4. Review & Submit</h2>
        <div className="ow-banner ow-banner-published">
          <strong>This event has been approved and is on sale.</strong>
        </div>
        <Link to="/organizer/events" className="tb-btn tb-btn-primary">
          Back to my events
        </Link>
      </div>
    );
  }

  return (
    <div className="ow-step-body">
      <h2>4. Review & Submit</h2>
      <p className="ow-hint">
        Double-check everything before sending it to the admin. Once
        submitted, the event moves to Pending status and can't be edited
        until it's approved or rejected.
      </p>

      {error && <div className="ow-error">{error}</div>}

      {checking && <div className="tb-loading">Checking submission requirements...</div>}

      {!checking && validation && (
        <div
          className={
            "ow-validation " + (validation.isValid ? "ow-validation-ok" : "ow-validation-bad")
          }
        >
          {validation.isValid ? (
            <div className="ow-validation-title">✅ Ready to submit</div>
          ) : (
            <>
              <div className="ow-validation-title">
                ⚠️ {validation.errors.length} issue(s) to fix
              </div>
              <ul>
                {validation.errors.map((e, i) => (
                  <li key={i}>{e}</li>
                ))}
              </ul>
            </>
          )}

          {validation.warnings?.length > 0 && (
            <div className="ow-validation-warnings">
              <div className="ow-validation-title">Note</div>
              <ul>
                {validation.warnings.map((w, i) => (
                  <li key={i}>{w}</li>
                ))}
              </ul>
            </div>
          )}
        </div>
      )}

      {event && (
        <div className="ow-review-summary">
          <div className="ow-review-row">
            {event.posterUrl && (
              <img className="ow-review-poster" src={event.posterUrl} alt="" />
            )}
            <div>
              <h3>{event.title}</h3>
              {event.shortDescription && <p>{event.shortDescription}</p>}
              <p className="ow-hint">
                {formatDateRange(event.startsAt, event.endsAt)}
              </p>
              <p className="ow-hint">
                {[event.locationName, event.address, event.city]
                  .filter(Boolean)
                  .join(", ") || "No venue yet"}
              </p>
            </div>
          </div>

          {event.ticketTypes?.length > 0 && (
            <table className="ow-table">
              <thead>
                <tr>
                  <th>Ticket type</th>
                  <th>Price</th>
                  <th>Quantity</th>
                </tr>
              </thead>
              <tbody>
                {event.ticketTypes.map((t) => (
                  <tr key={t.ticketTypeId}>
                    <td>{t.typeName}</td>
                    <td>{formatPrice(t.price)}</td>
                    <td>{t.quantity}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}

          {event.seatingChart && (
            <p className="ow-hint">
              Seating chart: {event.seatingChart.zones?.length || 0} zone(s).
            </p>
          )}

          {event.refundPolicies?.length > 0 && (
            <p className="ow-hint">
              {event.refundPolicies.length} refund polic{event.refundPolicies.length === 1 ? "y" : "ies"} configured.
            </p>
          )}
        </div>
      )}

      <div className="ow-actions">
        <button type="button" className="tb-btn tb-btn-outline" onClick={onBack}>
          ← Back
        </button>
        <button type="button" className="tb-btn tb-btn-outline" onClick={handleSaveDraft}>
          💾 Save draft
        </button>
        <button
          type="button"
          className="tb-btn tb-btn-primary"
          onClick={handleSubmit}
          disabled={submitting || checking || (validation && !validation.isValid)}
        >
          {submitting ? "Submitting..." : "🚀 Submit for review"}
        </button>
      </div>
    </div>
  );
}
