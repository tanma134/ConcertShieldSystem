import { useCallback, useEffect, useRef, useState } from "react";
import { Link, useParams } from "react-router-dom";
import OrganizerShell from "./OrganizerShell";
import organizerOrderApi from "../../api/organizerOrderApi";
import { apiErrorMessage } from "../../utils/apiError";
import { formatPrice } from "../../utils/format";
import "./ConcertOrderListPage.css";

const PAGE_SIZE = 10;

const EMPTY_FILTERS = {
  search: "",
  status: "",
  fromDate: "",
  toDate: "",
};

function formatDate(value) {
  if (!value) return "—";

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;

  return date.toLocaleString();
}

function getResponseData(response) {
  return response?.data?.data ?? {};
}

export default function ConcertOrderListPage() {
  const { id: eventId } = useParams();

  const [draftFilters, setDraftFilters] = useState(EMPTY_FILTERS);
  const [appliedFilters, setAppliedFilters] = useState(EMPTY_FILTERS);
  const [page, setPage] = useState(1);

  const [result, setResult] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [filterError, setFilterError] = useState("");

  const fromDateRef = useRef(null);
  const toDateRef = useRef(null);

  const loadOrders = useCallback(async () => {
    setLoading(true);
    setError("");

    try {
      const response = await organizerOrderApi.getByEvent(eventId, {
        ...appliedFilters,
        page,
        pageSize: PAGE_SIZE,
      });

      setResult(getResponseData(response));
    } catch (err) {
      setResult(null);
      setError(
        apiErrorMessage(err, "Could not load orders for this event.")
      );
    } finally {
      setLoading(false);
    }
  }, [eventId, appliedFilters, page]);

  useEffect(() => {
    loadOrders();
  }, [loadOrders]);

  function handleChange(event) {
    const { name, value } = event.target;

    setDraftFilters((current) => ({
      ...current,
      [name]: value,
    }));

    setFilterError("");
  }

  function applyFilters(event) {
    event.preventDefault();

    if (
      draftFilters.fromDate &&
      draftFilters.toDate &&
      draftFilters.fromDate > draftFilters.toDate
    ) {
      setFilterError("From date must not be later than To date.");
      return;
    }

    setPage(1);
    setAppliedFilters({ ...draftFilters });
  }

  function clearFilters() {
    setDraftFilters(EMPTY_FILTERS);
    setAppliedFilters(EMPTY_FILTERS);
    setFilterError("");
    setPage(1);
  }

  const items = result?.items ?? [];
  const totalCount = result?.totalCount ?? 0;
  const totalPages = Math.max(1, result?.totalPages ?? 1);
  const eventName = result?.eventName || "Concert orders";

  return (
    <OrganizerShell title="Concert Order List">
      <main className="concert-orders-page">
        <header className="concert-orders-header">
          <div>
            <p className="concert-orders-eyebrow">Organizer · Orders</p>
            <h1>{eventName}</h1>
            <p className="concert-orders-description">
              View and monitor customer orders for this concert.
            </p>
          </div>

          <div className="concert-orders-header-actions">
            <Link to="/organizer/events" className="co-button co-button-outline">
              ← My Events
            </Link>

            <button
              type="button"
              className="co-button co-button-outline"
              onClick={loadOrders}
              disabled={loading}
            >
              {loading ? "Loading…" : "Refresh"}
            </button>
          </div>
        </header>

        <form className="concert-orders-filters" onSubmit={applyFilters}>
          <label className="co-field">
            <span>Search</span>
            <input
              type="search"
              name="search"
              value={draftFilters.search}
              onChange={handleChange}
              placeholder="Order ID, customer name or email"
            />
          </label>

          <label className="co-field">
            <span>Status</span>
            <select
              name="status"
              value={draftFilters.status}
              onChange={handleChange}
            >
              <option value="">All statuses</option>
              <option value="Pending">Pending</option>
              <option value="Paid">Paid</option>
              <option value="Cancelled">Cancelled</option>
              <option value="Expired">Expired</option>
              <option value="Refunded">Refunded</option>
            </select>
          </label>

          <label className="co-field">
            <span>From date</span>
            <input
              ref={fromDateRef}
              type="date"
              name="fromDate"
              value={draftFilters.fromDate}
              onChange={handleChange}
              onClick={() => fromDateRef.current?.showPicker?.()}
            />
          </label>

          <label className="co-field">
            <span>To date</span>
            <input
              ref={toDateRef}
              type="date"
              name="toDate"
              value={draftFilters.toDate}
              onChange={handleChange}
              onClick={() => toDateRef.current?.showPicker?.()}
            />
          </label>

          <div className="co-filter-actions">
            <button type="submit" className="co-button co-button-primary">
              Apply filters
            </button>

            <button
              type="button"
              className="co-button co-button-outline"
              onClick={clearFilters}
            >
              Clear
            </button>
          </div>
        </form>

        {filterError && (
          <div className="co-alert co-alert-error" role="alert">
            {filterError}
          </div>
        )}

        {error && (
          <div className="co-alert co-alert-error" role="alert">
            <span>{error}</span>
            <button type="button" onClick={loadOrders}>
              Retry
            </button>
          </div>
        )}

        <section className="concert-orders-card">
          <div className="concert-orders-card-heading">
            <div>
              <h2>Customer orders</h2>
              <p>{totalCount} order(s) found</p>
            </div>
          </div>

          {loading ? (
            <div className="co-state">Loading orders…</div>
          ) : !error && items.length === 0 ? (
            <div className="co-state">
              No orders match the selected filters.
            </div>
          ) : (
            !error && (
              <div className="co-table-wrap">
                <table className="co-table">
                  <thead>
                    <tr>
                      <th>Order</th>
                      <th>Customer</th>
                      <th>Order date</th>
                      <th>Tickets</th>
                      <th>Total</th>
                      <th>Payment method</th>
                      <th>Status</th>
                    </tr>
                  </thead>

                  <tbody>
                    {items.map((order) => (
                      <tr key={order.orderId}>
                        <td>#{order.orderId}</td>
                        <td>
                          <div className="co-customer-name">
                            {order.customerName || "—"}
                          </div>
                          <div className="co-customer-email">
                            {order.customerEmail || ""}
                          </div>
                        </td>
                        <td>{formatDate(order.orderDate)}</td>
                        <td>{order.ticketCount ?? 0}</td>
                        <td>{formatPrice(order.finalAmount ?? 0)}</td>
                        <td>{order.paymentMethod || "—"}</td>
                        <td>
                          <span
                            className={`co-status co-status-${String(
                              order.status || "unknown"
                            )
                              .toLowerCase()
                              .replaceAll(" ", "-")}`}
                          >
                            {order.status || "Unknown"}
                          </span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )
          )}

          <footer className="co-pagination">
            <span>
              Page {page} of {totalPages}
            </span>

            <div>
              <button
                type="button"
                className="co-button co-button-outline"
                onClick={() => setPage((current) => Math.max(1, current - 1))}
                disabled={loading || page <= 1}
              >
                Previous
              </button>

              <button
                type="button"
                className="co-button co-button-outline"
                onClick={() =>
                  setPage((current) => Math.min(totalPages, current + 1))
                }
                disabled={loading || page >= totalPages}
              >
                Next
              </button>
            </div>
          </footer>
        </section>
      </main>
    </OrganizerShell>
  );
}