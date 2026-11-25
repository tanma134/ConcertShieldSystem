import { useCallback, useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import AdminShell from "./AdminShell";
import ticketIssuanceRecoveryApi from "../../api/ticketIssuanceRecoveryApi";
import { useToast } from "../../components/ToastProvider";
import "./TicketIssuanceRecoveryQueuePage.css";

const PAGE_SIZE = 10;

function read(item, camelName, pascalName) {
  return item?.[camelName] ?? item?.[pascalName];
}

function formatDate(value) {
  if (!value) return "-";

  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "-" : date.toLocaleString();
}

function formatMoney(value) {
  if (value == null) return "-";

  return new Intl.NumberFormat("vi-VN", {
    style: "currency",
    currency: "VND",
    maximumFractionDigits: 0,
  }).format(value);
}

function toIsoStartOfDay(value) {
  return value ? new Date(`${value}T00:00:00`).toISOString() : undefined;
}

function toIsoEndOfDay(value) {
  return value ? new Date(`${value}T23:59:59.999`).toISOString() : undefined;
}

export default function TicketIssuanceRecoveryQueuePage() {
  const navigate = useNavigate();
  const toast = useToast();

  const [items, setItems] = useState([]);
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  const [filters, setFilters] = useState({ from: "", to: "" });
  const [appliedFilters, setAppliedFilters] = useState({ from: "", to: "" });

  const loadQueue = useCallback(
    async (showToast = false) => {
      setLoading(true);
      setError("");

      try {
        const response = await ticketIssuanceRecoveryApi.getQueue({
          page,
          pageSize: PAGE_SIZE,
          from: toIsoStartOfDay(appliedFilters.from),
          to: toIsoEndOfDay(appliedFilters.to),
        });

        const result = response?.data?.data ?? response?.data ?? {};

        setItems(result.items ?? result.Items ?? []);
        setTotalCount(result.totalCount ?? result.TotalCount ?? 0);

        if (showToast) {
          toast.success("Ticket recovery queue refreshed.");
        }
      } catch (requestError) {
        const message =
          requestError?.response?.data?.message ??
          requestError?.response?.data?.Message ??
          "Could not load the ticket issuance recovery queue.";

        setItems([]);
        setTotalCount(0);
        setError(message);
        toast.error(message);
      } finally {
        setLoading(false);
      }
    },
    [page, appliedFilters, toast]
  );

  useEffect(() => {
    loadQueue();
  }, [loadQueue]);

  function handleFilterChange(event) {
    const { name, value } = event.target;
    setFilters((current) => ({ ...current, [name]: value }));
  }

  function handleApplyFilters(event) {
    event.preventDefault();
    setPage(1);
    setAppliedFilters({ ...filters });
  }

  function handleClearFilters() {
    const emptyFilters = { from: "", to: "" };
    setFilters(emptyFilters);
    setAppliedFilters(emptyFilters);
    setPage(1);
  }

  function handleResolve(item) {
    const orderId = read(item, "orderId", "OrderId");

    if (!orderId) {
      toast.error("Order ID is missing.");
      return;
    }

    navigate(
      `/admin/payment-reconciliation?orderId=${encodeURIComponent(orderId)}`
    );
  }

  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  return (
    <AdminShell title="Ticket Issuance Recovery Queue">
      <main className="failed-queue-page">
        <header className="failed-queue-header">
          <div>
            <p className="failed-queue-eyebrow">Ticket operations</p>
            <h1>Ticket Issuance Recovery Queue</h1>
            <p className="failed-queue-description">
              Paid orders with missing tickets or QR codes.
            </p>
          </div>

          <button
            type="button"
            className="failed-queue-refresh"
            onClick={() => loadQueue(true)}
            disabled={loading}
          >
            {loading ? <span className="app-loading-spinner" role="status" aria-label="Loading" /> : "Refresh"}
          </button>
        </header>

        <form className="failed-queue-filters" onSubmit={handleApplyFilters}>
          <label>
            From date
            <input
              type="date"
              name="from"
              value={filters.from}
              onChange={handleFilterChange}
            />
          </label>

          <label>
            To date
            <input
              type="date"
              name="to"
              value={filters.to}
              onChange={handleFilterChange}
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

        <section className="failed-queue-card">
          <div className="failed-queue-card-heading">
            <div>
              <h2>Orders requiring ticket recovery</h2>
              <p>{totalCount} order(s)</p>
            </div>
          </div>

          {loading ? (
            <div className="failed-queue-state"><span className="app-loading-spinner" role="status" aria-label="Loading" /></div>
          ) : error ? (
            <div className="failed-queue-state">{error}</div>
          ) : items.length === 0 ? (
            <div className="failed-queue-state">
              No paid orders need ticket recovery.
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
                    <th>Tickets</th>
                    <th>Tickets without QR</th>
                    <th>Issue</th>
                    <th>Action</th>
                  </tr>
                </thead>

                <tbody>
                  {items.map((item) => {
                    const transactionId = read(
                      item,
                      "paymentTransactionId",
                      "PaymentTransactionId"
                    );

                    const reasons = read(item, "reasons", "Reasons");

                    return (
                      <tr key={transactionId}>
                        <td>
                          {formatDate(
                            read(item, "requestedAt", "RequestedAt")
                          )}
                        </td>

                        <td>{transactionId ?? "-"}</td>
                        <td>
                          #{read(item, "orderId", "OrderId") ?? "-"}
                        </td>
                        <td>{read(item, "eventId", "EventId") ?? "-"}</td>
                        <td>{read(item, "gateway", "Gateway") ?? "-"}</td>
                        <td>
                          {formatMoney(
                            read(item, "paymentAmount", "PaymentAmount")
                          )}
                        </td>

                        <td>
                          {read(item, "issuedTicketCount", "IssuedTicketCount") ??
                            0}
                          {" / "}
                          {read(
                            item,
                            "expectedTicketCount",
                            "ExpectedTicketCount"
                          ) ?? 0}
                        </td>

                        <td>
                          {read(
                            item,
                            "ticketsWithoutQrCount",
                            "TicketsWithoutQrCount"
                          ) ?? 0}
                        </td>

                        <td>
                          {Array.isArray(reasons) && reasons.length > 0
                            ? reasons.join("; ")
                            : "Ticket or QR is missing."}
                        </td>

                        <td>
                          <button
                            type="button"
                            className="failed-queue-refresh"
                            onClick={() => handleResolve(item)}
                          >
                            Resolve
                          </button>
                        </td>
                      </tr>
                    );
                  })}
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
