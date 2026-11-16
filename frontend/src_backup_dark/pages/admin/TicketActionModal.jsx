import { useState } from "react";
import ticketApi from "../../api/ticketApi";
import "./TicketActionModal.css";

export default function TicketActionModal({
  action,
  ticket,
  onClose,
  onSuccess,
}) {
  const [reason, setReason] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  const isReissue = action === "reissue";
  const title = isReissue ? "Re-issue Ticket" : "Revoke Ticket";

  async function handleSubmit(event) {
    event.preventDefault();
    setError("");

    const trimmedReason = reason.trim();

    if (!trimmedReason) {
      setError("Please enter a reason.");
      return;
    }

    try {
      setLoading(true);

      const response = isReissue
        ? await ticketApi.reissueTicket(ticket.ticketId, trimmedReason)
        : await ticketApi.revokeTicket(ticket.ticketId, trimmedReason);

      const result = response.data?.data ?? response.data;

      await onSuccess?.(
        result,
        response.data?.message || `${title} completed successfully.`
      );

      onClose();
    } catch (err) {
      setError(
        err.response?.data?.message ||
          `Could not ${isReissue ? "re-issue" : "revoke"} this ticket.`
      );
    } finally {
      setLoading(false);
    }
  }

  return (
    <div
      className="ticket-action-backdrop"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget && !loading) onClose();
      }}
    >
      <section
        className="ticket-action-modal"
        role="dialog"
        aria-modal="true"
        aria-labelledby="ticket-action-title"
      >
        <button
          className="ticket-action-close"
          type="button"
          onClick={onClose}
          disabled={loading}
          aria-label="Close dialog"
        >
          ×
        </button>

        <p className="ticket-action-eyebrow">
          Ticket #{ticket.ticketId}
        </p>

        <h2 id="ticket-action-title">{title}</h2>

        <p className="ticket-action-description">
          {isReissue
            ? "A replacement ticket with a new QR identity will be created. The current ticket will be revoked."
            : "The ticket will be invalidated and its QR code can no longer be used."}
        </p>

        {error && (
          <div className="ticket-action-error" role="alert">
            {error}
          </div>
        )}

        <form onSubmit={handleSubmit}>
          <label htmlFor="ticket-action-reason">Reason</label>
          <textarea
            id="ticket-action-reason"
            value={reason}
            onChange={(event) => setReason(event.target.value)}
            maxLength={500}
            rows={4}
            required
            placeholder="Enter the reason for this action"
          />

          <div className="ticket-action-buttons">
            <button
              type="button"
              className="ticket-action-button ticket-action-cancel"
              onClick={onClose}
              disabled={loading}
            >
              Cancel
            </button>

            <button
              type="submit"
              className={`ticket-action-button ${
                isReissue
                  ? "ticket-action-confirm-reissue"
                  : "ticket-action-confirm-revoke"
              }`}
              disabled={loading}
            >
              {loading
                ? "Processing..."
                : isReissue
                  ? "Confirm Re-issue"
                  : "Confirm Revoke"}
            </button>
          </div>
        </form>
      </section>
    </div>
  );
}