import { useEffect, useState } from "react";
import AdminShell from "./AdminShell";
import ticketApi from "../../api/ticketApi";
import { useToast } from "../../components/ToastProvider";
import "./TicketStatusHistoryPage.css";

const PAGE_SIZE = 20;

const TARGET_TYPES = [
  ["", "All target types"],
  ["Order", "Orders"],
  ["Ticket", "Tickets"],
  ["Seat", "Seats"],
  ["TicketTypeInventory", "Ticket inventory"],
  ["ZoneInventory", "Zone inventory"],
];

const ACTION_LABELS = {
  HoldCreated: "Hold created",
  HoldReleased: "Hold released",
  OrderCreated: "Order created",
  PaymentSucceeded: "Payment successful",
  InventorySold: "Inventory sold",
  SeatSold: "Seat sold",
  TicketIssued: "Ticket issued",
  TicketCheckedIn: "Ticket checked in",
  ReturnRequested: "Return requested",
  ReturnApproved: "Return approved",
  TicketRevoked: "Ticket revoked",
  TicketReissued: "Ticket reissued",
};

function formatDate(value) {
  if (!value) return "—";

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "—";

  return date.toLocaleString("en-US", {
    dateStyle: "medium",
    timeStyle: "short",
  });
}

function getActionLabel(action) {
  return (
    ACTION_LABELS[action] ||
    action?.replace(/([a-z])([A-Z])/g, "$1 $2") ||
    "Status updated"
  );
}

function getTargetLabel(item) {
  switch (item.targetType) {
    case "Order":
      return `Order #${item.targetId}`;
    case "Ticket":
      return `Ticket #${item.targetId}`;
    case "Seat":
      return `Seat #${item.targetId}`;
    case "TicketTypeInventory":
      return `Ticket type #${item.targetId}`;
    case "ZoneInventory":
      return `Zone #${item.targetId}`;
    default:
      return `${item.targetType || "Target"} #${item.targetId}`;
  }
}

function getStatusChange(item) {
  if (!item.oldStatus && !item.newStatus) return "—";

  return `${item.oldStatus || "—"} → ${item.newStatus || "—"}`;
}

export default function TicketStatusHistoryPage() {
  const toast = useToast();

  const [items, setItems] = useState([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const [eventId, setEventId] = useState("");
  const [targetType, setTargetType] = useState("");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");

  const [appliedFilters, setAppliedFilters] = useState({});

  async function loadHistory(
    nextPage,
    filters,
    successMessage = ""
  ) {
    try {
      setLoading(true);
      setError("");

      const params = {
        page: nextPage,
        pageSize: PAGE_SIZE,
        eventId: filters.eventId || undefined,
        targetType: filters.targetType || undefined,
        from: filters.from
          ? new Date(filters.from).toISOString()
          : undefined,
        to: filters.to
          ? new Date(filters.to).toISOString()
          : undefined,
      };

      const response = await ticketApi.getAdminStatusHistories(params);
      const data = response.data?.data ?? response.data;

      setItems(Array.isArray(data?.items) ? data.items : []);
      setTotalCount(Number(data?.totalCount) || 0);
      setPage(Number(data?.page) || nextPage);

      if (successMessage) {
        toast.success(successMessage);
      }
    } catch (err) {
      const message =
        err.response?.data?.message ||
        err.response?.data?.Message ||
        "Could not load ticket status history.";

      setError(message);
      setItems([]);
      setTotalCount(0);
      toast.error(message);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadHistory(1, {});
    // Initial load only.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  function applyFilters(event) {
    event.preventDefault();

    const filters = {
      eventId: eventId.trim(),
      targetType,
      from,
      to,
    };

    setAppliedFilters(filters);
    loadHistory(1, filters, "Filters applied.");
  }

  function clearFilters() {
    const filters = {};

    setEventId("");
    setTargetType("");
    setFrom("");
    setTo("");
    setAppliedFilters(filters);
    loadHistory(1, filters, "Filters cleared.");
  }

  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  return (
    <AdminShell title="Ticket Status History">
      <div className="tsh-page">
        <header className="tsh-heading">
          <div>
            <p className="tsh-eyebrow">Ticketing operations</p>
            <h1>Ticket Status History</h1>
            <p>
              Review chronological changes to orders, tickets, seats, and
              ticket inventory.
            </p>
          </div>

          <div className="tsh-total">
            <strong>{totalCount.toLocaleString("en-US")}</strong>
            <span>records</span>
          </div>
        </header>

        <form className="tsh-filters" onSubmit={applyFilters}>
          <label className="tsh-filter-field">
            <span>Event ID</span>
            <input
              type="number"
              min="1"
              value={eventId}
              onChange={(event) => setEventId(event.target.value)}
              placeholder="All events"
            />
          </label>

          <label className="tsh-filter-field">
            <span>Target type</span>
            <select
              value={targetType}
              onChange={(event) => setTargetType(event.target.value)}
            >
              {TARGET_TYPES.map(([value, label]) => (
                <option key={value || "all"} value={value}>
                  {label}
                </option>
              ))}
            </select>
          </label>

          <label className="tsh-filter-field">
            <span>From</span>
            <input
              type="datetime-local"
              value={from}
              onChange={(event) => setFrom(event.target.value)}
            />
          </label>

          <label className="tsh-filter-field">
            <span>To</span>
            <input
              type="datetime-local"
              value={to}
              onChange={(event) => setTo(event.target.value)}
            />
          </label>

          <div className="tsh-filter-actions">
            <button type="submit" className="tsh-button tsh-button-primary">
              Apply filters
            </button>

            <button
              type="button"
              className="tsh-button tsh-button-secondary"
              onClick={clearFilters}
            >
              Clear
            </button>
          </div>
        </form>

        <section className="tsh-table-card">
          <div className="tsh-table-heading">
            <div>
              <h2>Activity timeline</h2>
              <p>Records are shown in chronological order.</p>
            </div>

            <span className="tsh-page-indicator">
              Page {page} of {totalPages}
            </span>
          </div>

          {loading ? (
            <div className="tsh-state">Loading status history...</div>
          ) : error ? null : items.length === 0 ? (
            <div className="tsh-state">
              No status history matches these filters.
            </div>
          ) : (
            <div className="tsh-table-scroll">
              <table className="tsh-table">
                <thead>
                  <tr>
                    <th>Time</th>
                    <th>Event</th>
                    <th>Target</th>
                    <th>Action</th>
                    <th>Status change</th>
                    <th>Details</th>
                  </tr>
                </thead>

                <tbody>
                  {items.map((item) => (
                    <tr key={item.statusHistoryId}>
                      <td className="tsh-time">
                        {formatDate(item.createdAt)}
                      </td>

                      <td>Event #{item.eventId}</td>

                      <td>
                        <span
                          className={`tsh-target tsh-target-${String(
                            item.targetType || "other"
                          ).toLowerCase()}`}
                        >
                          {getTargetLabel(item)}
                        </span>
                      </td>

                      <td>
                        <strong>{getActionLabel(item.action)}</strong>
                      </td>

                      <td>
                        <span className="tsh-status-change">
                          {getStatusChange(item)}
                        </span>
                      </td>

                      <td className="tsh-details">
                        {item.quantity != null && (
                          <span>Quantity: {item.quantity}</span>
                        )}
                        {item.orderId && <span>Order #{item.orderId}</span>}
                        {item.ticketId && <span>Ticket #{item.ticketId}</span>}
                        {item.actorRole && <span>By {item.actorRole}</span>}
                        {!item.quantity &&
                          !item.orderId &&
                          !item.ticketId &&
                          !item.actorRole &&
                          "—"}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          <footer className="tsh-pagination">
            <span>
              {totalCount > 0
                ? `Showing ${(page - 1) * PAGE_SIZE + 1}–${Math.min(
                    page * PAGE_SIZE,
                    totalCount
                  )} of ${totalCount}`
                : "0 records"}
            </span>

            <div className="tsh-pagination-buttons">
              <button
                type="button"
                className="tsh-button tsh-button-secondary"
                disabled={loading || page <= 1}
                onClick={() => loadHistory(page - 1, appliedFilters)}
              >
                Previous
              </button>

              <button
                type="button"
                className="tsh-button tsh-button-secondary"
                disabled={loading || page >= totalPages}
                onClick={() => loadHistory(page + 1, appliedFilters)}
              >
                Next
              </button>
            </div>
          </footer>
        </section>
      </div>
    </AdminShell>
  );
}