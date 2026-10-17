import { useCallback, useEffect, useRef, useState } from "react";
import AdminShell from "./AdminShell";
import failedTransactionApi from "../../api/failedTransactionApi";
import "./FailedTransactionQueuePage.css";

const PAGE_SIZE = 10;

const EMPTY_FILTERS = {
  orderId: "",
  eventId: "",
  gateway: "",
  fromDate: "",
  toDate: "",
};

function unwrapResponse(response) {
  return response?.data?.data ?? response?.data ?? {};
}

function read(item, camelName, pascalName) {
  return item?.[camelName] ?? item?.[pascalName];
}

function formatDate(value) {
  if (!value) return "—";

  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
}

function formatMoney(value) {
  if (value == null) return "—";

  return new Intl.NumberFormat("vi-VN", {
    style: "currency",
    currency: "VND",
    maximumFractionDigits: 0,
  }).format(value);
}

function getIssueDescription(item) {
  const missingTickets = Number(
    read(item, "missingTicketCount", "MissingTicketCount") ?? 0
  );
  const ticketsWithoutQr = Number(
    read(item, "ticketsWithoutQrCount", "TicketsWithoutQrCount") ?? 0
  );

  if (missingTickets > 0 && ticketsWithoutQr > 0) {
    return `${missingTickets} missing ticket(s); ${ticketsWithoutQr} ticket(s) without QR`;
  }

  if (missingTickets > 0) {
    return `${missingTickets} missing ticket(s)`;
  }

  if (ticketsWithoutQr > 0) {
    return `${ticketsWithoutQr} ticket(s) without QR`;
  }

  return "Ticket issuance incomplete";
}

function matchesDateRange(item, filters) {
  const requestedAt = read(item, "requestedAt", "RequestedAt");

  if (!requestedAt) {
    return !filters.fromDate && !filters.toDate;
  }

  const transactionDate = new Date(requestedAt);
  if (Number.isNaN(transactionDate.getTime())) return false;

  if (filters.fromDate) {
    const from = new Date(`${filters.fromDate}T00:00:00`);
    if (transactionDate < from) return false;
  }

  if (filters.toDate) {
    const to = new Date(`${filters.toDate}T23:59:59.999`);
    if (transactionDate > to) return false;
  }

  return true;
}

function matchesFilters(item, filters) {
  const orderId = String(read(item, "orderId", "OrderId") ?? "");
  const eventId = String(read(item, "eventId", "EventId") ?? "");
  const gateway = String(
    read(item, "gatewayName", "GatewayName") ??
      read(item, "gateway", "Gateway") ??
      ""
  );

  return (
    (!filters.orderId || orderId.includes(filters.orderId.trim())) &&
    (!filters.eventId || eventId.includes(filters.eventId.trim())) &&
    (!filters.gateway ||
      gateway.toLowerCase().includes(filters.gateway.trim().toLowerCase())) &&
    matchesDateRange(item, filters)
  );
}

export default function FailedTransactionQueuePage() {
  const [items, setItems] = useState([]);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [filters, setFilters] = useState(EMPTY_FILTERS);
  const [appliedFilters, setAppliedFilters] = useState(EMPTY_FILTERS);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  const fromDateRef = useRef(null);
  const toDateRef = useRef(null);

  const loadQueue = useCallback(async () => {
    setLoading(true);
    setError("");

    try {
      const response = await failedTransactionApi.getQueue(page, PAGE_SIZE);
      const result = unwrapResponse(response);

      setItems(result.items ?? result.Items ?? []);
      setTotalPages(result.totalPages ?? result.TotalPages ?? 1);
    } catch (err) {
      setItems([]);
      setError(
        err?.response?.data?.message ??
          err?.response?.data?.Message ??
          "Could not load the failed transaction queue."
      );
    } finally {
      setLoading(false);
    }
  }, [page]);

  useEffect(() => {
    loadQueue();
  }, [loadQueue]);

  function handleFilterChange(event) {
    const { name, value } = event.target;
    setFilters((current) => ({ ...current, [name]: value }));
  }

  function handleFilterSubmit(event) {
    event.preventDefault();
    setPage(1);
    setAppliedFilters({ ...filters });
  }

  function handleClearFilters() {
    setFilters(EMPTY_FILTERS);
    setAppliedFilters(EMPTY_FILTERS);
    setPage(1);
  }

  const filteredItems = items.filter((item) =>
    matchesFilters(item, appliedFilters)
  );

  return (
    <AdminShell title="Failed Transaction Queue">
      <main className="failed-queue-page">
        <header className="failed-queue-header">
          <div>
            <p className="failed-queue-eyebrow">Payment operations</p>
            <h1>Failed Transaction Queue</h1>
            <p className="failed-queue-description">
              Successful payments with missing tickets or QR tokens.
            </p>
          </div>

          <button
            type="button"
            className="failed-queue-refresh"
            onClick={loadQueue}
            disabled={loading}
          >
            {loading ? "Loading…" : "Refresh"}
          </button>
        </header>

        <form className="failed-queue-filters" onSubmit={handleFilterSubmit}>
          <label>
            From date
            <input
              ref={fromDateRef}
              type="date"
              name="fromDate"
              value={filters.fromDate}
              onChange={handleFilterChange}
              onClick={() => fromDateRef.current?.showPicker?.()}
            />
          </label>

          <label>
            To date
            <input
              ref={toDateRef}
              type="date"
              name="toDate"
              value={filters.toDate}
              onChange={handleFilterChange}
              onClick={() => toDateRef.current?.showPicker?.()}
            />
          </label>

          <label>
            Order ID
            <input
              type="text"
              name="orderId"
              value={filters.orderId}
              onChange={handleFilterChange}
              placeholder="Search order ID"
            />
          </label>

          <label>
            Event ID
            <input
              type="text"
              name="eventId"
              value={filters.eventId}
              onChange={handleFilterChange}
              placeholder="Search event ID"
            />
          </label>

          <label>
            Gateway
            <input
              type="text"
              name="gateway"
              value={filters.gateway}
              onChange={handleFilterChange}
              placeholder="Search gateway"
            />
          </label>

          <div className="failed-queue-filter-actions">
            <button type="submit" className="failed-queue-apply">
              Apply filters
            </button>
            <button
              type="button"
              className="failed-queue-clear"
              onClick={handleClearFilters}
            >
              Clear
            </button>
          </div>
        </form>

        {error && (
          <div className="failed-queue-alert" role="alert">
            {error}
          </div>
        )}

        <section className="failed-queue-card">
          <div className="failed-queue-card-heading">
            <div>
              <h2>Transactions</h2>
              <p>{filteredItems.length} transaction(s) on this page</p>
            </div>
          </div>

          {loading ? (
            <div className="failed-queue-state">Loading transactions…</div>
          ) : filteredItems.length === 0 ? (
            <div className="failed-queue-state">
              No failed transactions match the selected filters.
            </div>
          ) : (
            <div className="failed-queue-table-wrap">
              <table className="failed-queue-table">
                <thead>
                  <tr>
                    <th>Payment date</th>
                    <th>Transaction ID</th>
                    <th>Order ID</th>
                    <th>Event ID</th>
                    <th>Gateway</th>
                    <th>Amount</th>
                    <th>Payment status</th>
                    <th>Issuance issue</th>
                  </tr>
                </thead>

                <tbody>
                  {filteredItems.map((item, index) => (
                    <tr
                      key={
                        read(
                          item,
                          "paymentTransactionId",
                          "PaymentTransactionId"
                        ) ?? index
                      }
                    >
                      <td>
                        {formatDate(read(item, "requestedAt", "RequestedAt"))}
                      </td>
                      <td>
                        {read(
                          item,
                          "paymentTransactionId",
                          "PaymentTransactionId"
                        ) ?? "—"}
                      </td>
                      <td>{read(item, "orderId", "OrderId") ?? "—"}</td>
                      <td>{read(item, "eventId", "EventId") ?? "—"}</td>
                      <td>
                        {read(item, "gatewayName", "GatewayName") ??
                          read(item, "gateway", "Gateway") ??
                          "—"}
                      </td>
                      <td>
                        {formatMoney(
                          read(item, "paymentAmount", "PaymentAmount") ??
                            read(item, "amount", "Amount")
                        )}
                      </td>
                      <td>
                        <span className="failed-queue-status">
                          {read(item, "paymentStatus", "PaymentStatus") ??
                            "Success"}
                        </span>
                      </td>
                      <td>{getIssueDescription(item)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          <footer className="failed-queue-pagination">
            <button
              type="button"
              onClick={() => setPage((current) => Math.max(1, current - 1))}
              disabled={loading || page <= 1}
            >
              Previous
            </button>

            <span>
              Page {page} of {totalPages}
            </span>

            <button
              type="button"
              onClick={() =>
                setPage((current) => Math.min(totalPages, current + 1))
              }
              disabled={loading || page >= totalPages}
            >
              Next
            </button>
          </footer>
        </section>
      </main>
    </AdminShell>
  );
}