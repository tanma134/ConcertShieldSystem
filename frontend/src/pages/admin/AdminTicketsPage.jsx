import { useCallback, useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import AdminShell from "./AdminShell";
import TicketActionModal from "./TicketActionModal";
import ticketApi from "../../api/ticketApi";
import "./AdminTicketsPage.css";

const PAGE_SIZE = 20;

function unwrap(response) {
  return response.data?.data ?? response.data;
}

function formatDate(value) {
  if (!value) return "—";

  const date = new Date(value);

  return Number.isNaN(date.getTime())
    ? "—"
    : date.toLocaleString("en-US", {
        dateStyle: "medium",
        timeStyle: "short",
      });
}

export default function AdminTicketsPage() {
  const navigate = useNavigate();

  const [tickets, setTickets] = useState([]);
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");

  const [ticketId, setTicketId] = useState("");
  const [orderId, setOrderId] = useState("");
  const [eventId, setEventId] = useState("");
  const [status, setStatus] = useState("");
  const [search, setSearch] = useState("");
  const [filters, setFilters] = useState({});

  const [selectedAction, setSelectedAction] = useState(null);

  const loadTickets = useCallback(async () => {
    try {
      setLoading(true);
      setError("");

      const response = await ticketApi.getAdminTickets({
        page,
        pageSize: PAGE_SIZE,
        ticketId: filters.ticketId || undefined,
        orderId: filters.orderId || undefined,
        eventId: filters.eventId || undefined,
        status: filters.status || undefined,
        search: filters.search || undefined,
      });

      const data = unwrap(response);

      setTickets(Array.isArray(data?.items) ? data.items : []);
      setTotalCount(Number(data?.totalCount) || 0);
    } catch (err) {
      setError(err.response?.data?.message || "Could not load tickets.");
      setTickets([]);
      setTotalCount(0);
    } finally {
      setLoading(false);
    }
  }, [filters, page]);

  useEffect(() => {
    loadTickets();
  }, [loadTickets]);

  function applyFilters(event) {
    event.preventDefault();

    setPage(1);
    setNotice("");
    setFilters({
      ticketId: ticketId.trim(),
      orderId: orderId.trim(),
      eventId: eventId.trim(),
      status,
      search: search.trim(),
    });
  }

  function clearFilters() {
    setTicketId("");
    setOrderId("");
    setEventId("");
    setStatus("");
    setSearch("");
    setPage(1);
    setNotice("");
    setFilters({});
  }

  function canRevoke(ticket) {
    return (
      String(ticket.status || "").toLowerCase() === "active" &&
      !ticket.checkedInAt
    );
  }

  function canReissue(ticket) {
    const ticketStatus = String(ticket.status || "").toLowerCase();

    return (
      ["active", "revoked"].includes(ticketStatus) &&
      !ticket.checkedInAt
    );
  }

  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  return (
    <AdminShell title="Ticket Management">
      <main className="admin-tickets-page">
        <header className="admin-tickets-heading">
          <div>
            <p className="admin-tickets-eyebrow">Ticketing operations</p>
            <h1>Ticket Management</h1>
            <p>
              Search tickets, review their details, and manage ticket actions.
            </p>
          </div>

          <div className="admin-tickets-count">
            <strong>{totalCount.toLocaleString("en-US")}</strong>
            <span>tickets</span>
          </div>
        </header>

        {notice && (
          <div className="admin-tickets-notice" role="status">
            {notice}
          </div>
        )}

        {error && (
          <div className="admin-tickets-error" role="alert">
            {error}
          </div>
        )}

        <form className="admin-tickets-filters" onSubmit={applyFilters}>
          <label>
            <span>Ticket ID</span>
            <input
              type="number"
              min="1"
              value={ticketId}
              onChange={(event) => setTicketId(event.target.value)}
              placeholder="Any ticket"
            />
          </label>

          <label>
            <span>Order ID</span>
            <input
              type="number"
              min="1"
              value={orderId}
              onChange={(event) => setOrderId(event.target.value)}
              placeholder="Any order"
            />
          </label>

          <label>
            <span>Event ID</span>
            <input
              type="number"
              min="1"
              value={eventId}
              onChange={(event) => setEventId(event.target.value)}
              placeholder="Any event"
            />
          </label>

          <label>
            <span>Status</span>
            <select
              value={status}
              onChange={(event) => setStatus(event.target.value)}
            >
              <option value="">All statuses</option>
              <option value="Active">Active</option>
              <option value="ReturnPending">Return pending</option>
              <option value="Returned">Returned</option>
              <option value="Revoked">Revoked</option>
            </select>
          </label>

          <label className="admin-tickets-search">
            <span>Customer or ticket type</span>
            <input
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Search name, email, or ticket type"
            />
          </label>

          <div className="admin-tickets-filter-buttons">
            <button type="submit" className="admin-ticket-button primary">
              Apply filters
            </button>

            <button
              type="button"
              className="admin-ticket-button secondary"
              onClick={clearFilters}
            >
              Clear
            </button>
          </div>
        </form>

        <section className="admin-tickets-table-card">
          <div className="admin-tickets-table-heading">
            <h2>Tickets</h2>
            <span>
              Page {page} of {totalPages}
            </span>
          </div>

          {loading ? (
            <div className="admin-tickets-state">Loading tickets...</div>
          ) : tickets.length === 0 ? (
            <div className="admin-tickets-state">
              No tickets match these filters.
            </div>
          ) : (
            <div className="admin-tickets-table-scroll">
              <table className="admin-tickets-table">
                <thead>
                  <tr>
                    <th>Ticket</th>
                    <th>Customer</th>
                    <th>Event</th>
                    <th>Order</th>
                    <th>Ticket type</th>
                    <th>Status</th>
                    <th>Check-in</th>
                    <th>Actions</th>
                  </tr>
                </thead>

                <tbody>
                  {tickets.map((ticket) => (
                    <tr key={ticket.ticketId}>
                      <td>
                        <strong>#{ticket.ticketId}</strong>
                        <small>{ticket.ticketCode}</small>
                      </td>

                      <td>{ticket.ownerName || "—"}</td>

                      <td>
                        {ticket.eventName || `Event #${ticket.eventId}`}
                      </td>

                      <td>#{ticket.orderId}</td>
                      <td>{ticket.ticketTypeName || "—"}</td>

                      <td>
                        <span
                          className={`admin-ticket-status status-${String(
                            ticket.status || "unknown"
                          ).toLowerCase()}`}
                        >
                          {ticket.status || "Unknown"}
                        </span>
                      </td>

                      <td>{formatDate(ticket.checkedInAt)}</td>

                      <td>
                        <div className="admin-ticket-row-actions">
                          <button
                            type="button"
                            className="admin-ticket-button secondary"
                            onClick={() =>
                              navigate(`/admin/tickets/${ticket.ticketId}`)
                            }
                          >
                            View
                          </button>

                          <button
                            type="button"
                            className="admin-ticket-button revoke"
                            disabled={!canRevoke(ticket)}
                            onClick={() =>
                              setSelectedAction({
                                action: "revoke",
                                ticket,
                              })
                            }
                          >
                            Revoke
                          </button>

                          <button
                            type="button"
                            className="admin-ticket-button reissue"
                            disabled={!canReissue(ticket)}
                            onClick={() =>
                              setSelectedAction({
                                action: "reissue",
                                ticket,
                              })
                            }
                          >
                            Re-issue
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          <footer className="admin-tickets-pagination">
            <span>
              {totalCount > 0
                ? `Showing ${(page - 1) * PAGE_SIZE + 1}–${Math.min(
                    page * PAGE_SIZE,
                    totalCount
                  )} of ${totalCount}`
                : "0 tickets"}
            </span>

            <div>
              <button
                type="button"
                className="admin-ticket-button secondary"
                disabled={loading || page <= 1}
                onClick={() => setPage((current) => current - 1)}
              >
                Previous
              </button>

              <button
                type="button"
                className="admin-ticket-button secondary"
                disabled={loading || page >= totalPages}
                onClick={() => setPage((current) => current + 1)}
              >
                Next
              </button>
            </div>
          </footer>
        </section>

        {selectedAction && (
          <TicketActionModal
            action={selectedAction.action}
            ticket={selectedAction.ticket}
            onClose={() => setSelectedAction(null)}
            onSuccess={async (result, message) => {
              const replacementId =
                selectedAction.action === "reissue"
                  ? result?.ticketId
                  : null;

              setNotice(
                replacementId
                  ? `${message} Replacement ticket: #${replacementId}.`
                  : message
              );

              await loadTickets();
            }}
          />
        )}
      </main>
    </AdminShell>
  );
}