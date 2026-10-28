import { useCallback, useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import AdminShell from "./AdminShell";
import TicketActionModal from "./TicketActionModal";
import ticketApi from "../../api/ticketApi";
import { useToast } from "../../components/ToastProvider";
import "./AdminTicketDetailPage.css";

function unwrap(response) {
  return response.data?.data ?? response.data;
}

function formatDate(value) {
  if (!value) return "—";

  const date = new Date(value);

  return Number.isNaN(date.getTime())
    ? "—"
    : date.toLocaleString("en-US", {
        dateStyle: "full",
        timeStyle: "short",
      });
}

export default function AdminTicketDetailPage() {
  const { ticketId } = useParams();
  const toast = useToast();

  const [ticket, setTicket] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [selectedAction, setSelectedAction] = useState(null);

  const loadTicket = useCallback(async () => {
    try {
      setLoading(true);
      setError(false);

      const response = await ticketApi.getAdminTicketDetails(ticketId);
      const data = unwrap(response);

      if (!data) {
        setTicket(null);
        setError(true);
        toast.error("Ticket not found.");
        return;
      }

      setTicket(data);
    } catch (err) {
      const message =
        err?.response?.data?.message ||
        err?.response?.data?.Message ||
        "Could not load ticket details.";

      setError(true);
      setTicket(null);
      toast.error(message);
    } finally {
      setLoading(false);
    }
  }, [ticketId, toast]);

  useEffect(() => {
    loadTicket();
  }, [loadTicket]);

  if (loading) {
    return (
      <AdminShell title="Ticket Details">
        <main className="admin-ticket-detail-page">
          <div className="admin-ticket-detail-state">Loading ticket...</div>
        </main>
      </AdminShell>
    );
  }

  if (error || !ticket) {
    return (
      <AdminShell title="Ticket Details">
        <main className="admin-ticket-detail-page">
          <Link to="/admin/tickets" className="admin-ticket-back">
            ← Back to Ticket Management
          </Link>
        </main>
      </AdminShell>
    );
  }

  const ticketStatus = String(ticket.status || "").toLowerCase();

  const canRevoke = ticketStatus === "active" && !ticket.checkedInAt;

  const canReissue =
    ["active", "revoked"].includes(ticketStatus) && !ticket.checkedInAt;

  return (
    <AdminShell title={`Ticket #${ticket.ticketId}`}>
      <main className="admin-ticket-detail-page">
        <Link to="/admin/tickets" className="admin-ticket-back">
          ← Back to Ticket Management
        </Link>

        <header className="admin-ticket-detail-heading">
          <div>
            <p className="admin-ticket-detail-eyebrow">Ticket details</p>
            <h1>Ticket #{ticket.ticketId}</h1>

            <span
              className={`admin-ticket-detail-status status-${ticketStatus || "unknown"}`}
            >
              {ticket.status || "Unknown"}
            </span>
          </div>

          <div className="admin-ticket-detail-actions">
            <button
              type="button"
              className="admin-ticket-detail-button revoke"
              disabled={!canRevoke}
              onClick={() => setSelectedAction({ action: "revoke", ticket })}
            >
              Revoke Ticket
            </button>

            <button
              type="button"
              className="admin-ticket-detail-button reissue"
              disabled={!canReissue}
              onClick={() => setSelectedAction({ action: "reissue", ticket })}
            >
              Re-issue Ticket
            </button>
          </div>
        </header>

        {!canRevoke && !canReissue && (
          <div className="admin-ticket-detail-warning">
            This ticket cannot be revoked or re-issued because it has been
            checked in or its status does not allow these actions.
          </div>
        )}

        <section className="admin-ticket-detail-card">
          <h2>Ticket information</h2>

          <dl>
            <div>
              <dt>Ticket ID</dt>
              <dd>{ticket.ticketId}</dd>
            </div>

            <div>
              <dt>Ticket code</dt>
              <dd>{ticket.ticketCode || "—"}</dd>
            </div>

            <div>
              <dt>Ticket type</dt>
              <dd>{ticket.ticketTypeName || "—"}</dd>
            </div>

            <div>
              <dt>Seat ID</dt>
              <dd>{ticket.seatId ?? "—"}</dd>
            </div>

            <div>
              <dt>Event</dt>
              <dd>{ticket.eventName || `Event #${ticket.eventId}`}</dd>
            </div>

            <div>
              <dt>Event ID</dt>
              <dd>{ticket.eventId}</dd>
            </div>

            <div>
              <dt>Starts at</dt>
              <dd>{formatDate(ticket.startsAt)}</dd>
            </div>

            <div>
              <dt>Check-in time</dt>
              <dd>{formatDate(ticket.checkedInAt)}</dd>
            </div>
          </dl>
        </section>

        <section className="admin-ticket-detail-card">
          <h2>Order and owner</h2>

          <dl>
            <div>
              <dt>Order ID</dt>
              <dd>{ticket.orderId}</dd>
            </div>

            <div>
              <dt>Order status</dt>
              <dd>{ticket.orderStatus || "—"}</dd>
            </div>

            <div>
              <dt>Order date</dt>
              <dd>{formatDate(ticket.orderDate)}</dd>
            </div>

            <div>
              <dt>Owner</dt>
              <dd>{ticket.ownerName || "—"}</dd>
            </div>

            <div>
              <dt>Unit price</dt>
              <dd>
                {ticket.unitPrice != null
                  ? `${Number(ticket.unitPrice).toLocaleString("en-US")} VND`
                  : "—"}
              </dd>
            </div>
          </dl>
        </section>

        {selectedAction && (
          <TicketActionModal
            action={selectedAction.action}
            ticket={selectedAction.ticket}
            onClose={() => setSelectedAction(null)}
            onSuccess={async (result, message) => {
              const action = selectedAction.action;
              const replacementId =
                action === "reissue" ? result?.ticketId : null;

              const baseMessage =
                message ||
                (action === "reissue"
                  ? "Ticket re-issued successfully."
                  : "Ticket revoked successfully.");

              const successMessage = replacementId
                ? `${baseMessage} Replacement ticket: #${replacementId}.`
                : baseMessage;

              toast.success(successMessage);
              setSelectedAction(null);
              await loadTicket();
            }}
          />
        )}
      </main>
    </AdminShell>
  );
}